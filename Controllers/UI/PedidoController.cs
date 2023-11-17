using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using proyecto_ecommerce_deportivo_net.Data;
using proyecto_ecommerce_deportivo_net.Models.Validator;
/*LIBRERIAS PARA LA PAGINACION DE LISTAR PRODUCTOS */
using X.PagedList;

/*LIBRERIAS PARA SUBR IMAGENES */
using Firebase.Auth;
using Firebase.Storage;
using System.Web.WebPages;

/*LIBRERIAS NECESARIAS PARA EXPORTAR */
using DinkToPdf;
using DinkToPdf.Contracts;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.IO;
using System.Linq;
using OfficeOpenXml.Table;
using proyecto_ecommerce_deportivo_net.Models;
using proyecto_ecommerce_deportivo_net.Models.Entity;
using Microsoft.AspNetCore.Identity;

using System.Drawing;

namespace proyecto_ecommerce_deportivo_net.Controllers.UI
{
    public class PedidoController : Controller
    {
        private readonly ILogger<PedidoController> _logger;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        // Objeto para la exportación
        private readonly IConverter _converter;

        public PedidoController(ILogger<PedidoController> logger, UserManager<ApplicationUser> userManager, ApplicationDbContext context, IConverter converter)
        {
            _logger = logger;
            _userManager = userManager;
            _context = context;
            ModelState.Clear();


            _converter = converter; // PARA EXPORTAR
        }

        public IActionResult Index()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }

        /// <summary>
        /// Obtiene una lista paginada de pedidos. Si no se especifica un número de página, 
        /// se asume la primera página. Cada página muestra un máximo de 2 pedidos. 
        /// Esta función me ayuda a entender cómo implementar la paginación en ASP.NET Core 
        /// y cómo manejar parámetros opcionales en los controladores.
        /// </summary>
        public ActionResult ListaDePedidos(int? page)
        {
            int pageNumber = (page ?? 1); // Si no se especifica la página, asume la página 1
            int pageSize = 10; // maximo 10 pedidos por pagina


            pageNumber = Math.Max(pageNumber, 1);// Con esto se asegura de que pageNumber nunca sea menor que 1

            IPagedList listaPaginada = _context.DataPedido.ToPagedList(pageNumber, pageSize);

            return View("ListaDePedidos", listaPaginada);
        }


        /* metodos para exportar en pdf y excel desde aqui para abajo */
        public IActionResult ExportarPedidosEnPDF()
        {
            try
            {
                var pedidos = _context.DataPedido.ToList();
                var html = @"
            <html>
                <head>
                <meta charset='UTF-8'>
                    <style>
                        table {
                            width: 100%;
                            border-collapse: collapse;
                        }
                        th, td {
                            border: 1px solid black;
                            padding: 8px;
                            text-align: left;
                        }
                        th {
                            background-color: #f2f2f2;
                        }
                        img.logo {
                            position: absolute;
                            top: 0;
                            right: 0;
                            border-radius:50%;
                            height:3.3rem;
                            width:3.3rem;
                        }

                        h1 {
                            color: #40E0D0; /* Color celeste */
                        }
                    </style>
                </head>
                <body>
                    <img src='https://firebasestorage.googleapis.com/v0/b/proyectos-cb445.appspot.com/o/img_logo_athletix.png?alt=media&token=a32e429b-4ece-45d2-bf00-85a8f9081a9c&_gl=1*14iryjj*_ga*MTcyOTkyMjIwMS4xNjk2NDU2NzU2*_ga_CW55HF8NVT*MTY5ODAxNDc2Mi4yLjEuMTY5ODAxNDg0Ny40OC4wLjA.' alt='Logo' width='100' class='logo'/>
                    <h1>Reporte de Pedidos</h1>
                    <table>
                        <tr>
                            <th>ID</th>
                            <th>UserID</th>
                            <th>Total (en soles)</th>
                       
                            <th>Status</th>
                        </tr>";

                foreach (var pedido in pedidos)
                {

                    html += $@"
                <tr>
                    <td>{pedido.ID}</td>
                    <td>{pedido.UserID}</td>
                    <td>{pedido.Total}</td>
                 
                    <td>{pedido.Status}</td>
   
                </tr>";
                }

                html += @"
                    </table>
                </body>
            </html>";

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                };
                var objectSettings = new ObjectSettings { HtmlContent = html };
                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };
                var file = _converter.Convert(pdf);

                return File(file, "application/pdf", "Pedidos.pdf");

            }
            catch (Exception ex)
            {
                // Loguear el error para obtener más detalles
                _logger.LogError(ex, "Error al exportar Pedidos a PDF");
                // Retornar un mensaje de error al usuario
                return StatusCode(500, "Ocurrió un error al exportar los Pedidos a PDF. Por favor, inténtelo de nuevo más tarde.");
            }
        }


        public IActionResult ExportarPedidosEnExcel()
        {
            try
            {
                var resultados = (from p in _context.DataPedido
                                  join d in _context.DataDetallePedido on p.ID equals d.pedido.ID
                                  join pa in _context.DataPago on p.pago.Id equals pa.Id
                                  select new
                                  {
                                      IDPedido = p.ID,
                                      UserID = p.UserID,
                                      Total = p.Total,
                                      Status = p.Status,
                                      FechaDePago = pa.PaymentDate,
                                      NombreTarjeta = pa.NombreTarjeta,
                                      //Ultimos4DigitosTarjeta = pa.NumeroTarjeta.Length > 4 ? pa.NumeroTarjeta.Substring(pa.NumeroTarjeta.Length - 4) : pa.NumeroTarjeta,
                                      DigitosTarjeta = pa.NumeroTarjeta,
                                      MontoPagado = pa.MontoTotal,
                                      IDProducto = d.Producto.id,
                                      Cantidad = d.Cantidad,
                                      PrecioUnitario = d.Precio
                                  }).ToList();

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Pedidos");

                // Agregando un título arriba de la tabla
                worksheet.Cells[1, 1].Value = "Reporte de Pedidos";
                worksheet.Cells[1, 1].Style.Font.Size = 20;
                worksheet.Cells[1, 1].Style.Font.Bold = true;

                // Cargar los datos en la fila 3 para dejar espacio para el título de Reporte de Pedidos
                worksheet.Cells[3, 1].LoadFromCollection(resultados, true);

                // Dar formato a la tabla Reporte de Pedidos
                var dataRange = worksheet.Cells[2, 1, worksheet.Dimension.End.Row, worksheet.Dimension.End.Column];
                var table = worksheet.Tables.Add(dataRange, "Pedidos");
                table.ShowHeader = true;
                table.TableStyle = TableStyles.Light6;

                // Estilo para los encabezados de las columnas 
                worksheet.Cells[3, 1, 3, worksheet.Dimension.End.Column].Style.Font.Bold = true;
                worksheet.Cells[3, 1, 3, worksheet.Dimension.End.Column].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[3, 1, 3, worksheet.Dimension.End.Column].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                worksheet.Cells[3, 1, 3, worksheet.Dimension.End.Column].Style.Font.Color.SetColor(System.Drawing.Color.DarkBlue);

                // Ajustar el ancho de las columnas automáticamente
                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Pedidos.xlsx");
            }
            catch (Exception ex)
            {
                // Loguear el error para obtener más detalles
                _logger.LogError(ex, "Error al exportar pedidos a Excel");
                // Retornar un mensaje de error al usuario
                return StatusCode(500, "Ocurrió un error al exportar los pedidos a Excel. Por favor, inténtelo de nuevo más tarde.");
            }
        }

        /* Para exportar individualmente los Pedidos en pdf */
        public async Task<ActionResult> ExportarUnSoloPedidoEnPDF(int? id)
        {
            try
            {
                if (id == null)
                {
                    return NotFound($"El pedido con ID {id} no fue encontrado, por eso no se puede exportar en PDF.");
                }

                Pedido? pedido = await _context.DataPedido.FindAsync(id);

                if (pedido == null)
                {
                    return NotFound($"El pedido con ID {id} no fue encontrado, por eso no se puede exportar en PDF.");
                }

                // Obtener la información del cliente - color al del excel --- background-color: #87CEFA ---------- background-color: 40E0D0;
                ApplicationUser? cliente = await _context.Users.FirstOrDefaultAsync(u => u.UserName == pedido.UserID);

                if (cliente == null)
                {
                    return NotFound($"El cliente con ID {pedido.UserID} no fue encontrado en la tabla de Clientes.");
                }

                var detalles = (from detalle in _context.DataDetallePedido
                                join producto in _context.Producto on detalle.Producto.id equals producto.id
                                where detalle.pedido.ID == pedido.ID
                                select new
                                {
                                    Cantidad = detalle.Cantidad,
                                    PrecioUnitario = detalle.Precio,
                                    NombreProducto = producto.Nombre,
                                    DescripcionProducto = producto.Descripcion,
                                    Importe = detalle.Cantidad * detalle.Precio
                                }).ToList();

                var html = $@"
                    <html>
                        <head>
                            <meta charset='UTF-8'>
                            <style>
                                body {{
                                    font-family: 'Arial', sans-serif;
                                    margin: 40px;
                                }}
                                header {{
                                    display: flex;
                                    justify-content: space-between;
                                    align-items: center;
                                    border-bottom: 2px solid #87CEFA;
                                    padding-bottom: 20px;
                                    margin-bottom: 40px;
                                }}
                                header img.logo {{
                                    height: 80px;
                                    width: auto;
                                }}
                                header .company-details {{
                                    text-align: right;
                                }}
                                .client-info {{
                                    background-color: #f2f2f2;
                                    padding: 10px;
                                    margin-bottom: 20px;
                                    border-radius: 5px;
                                }}
                                .invoice-details {{
                                    margin-bottom: 40px;
                                }}
                                table {{
                                    width: 100%;
                                    border-collapse: collapse;
                                }}
                                th, td {{
                                    border: 1px solid black;
                                    padding: 8px;
                                    text-align: left;
                                }}
                                th {{
                                    background-color: #f2f2f2;
                                }}
                                .total {{
                                    text-align: right;
                                    margin-top: 30px;
                                }}
                                .footer {{
                                    margin-top: 50px;
                                    font-size: 12px;
                                    color: #888;
                                }}

                                .highlighted {{
                        
                                    background-color: #87CEFA;
                                    color: black;
                                    font-weight: bold;
                                    border: 1px solid #87CEFA;
                                }}
                                
                            </style>
                        </head>
                        <body>
                            <header>
                                <img src='https://firebasestorage.googleapis.com/v0/b/proyectos-cb445.appspot.com/o/img_logo_athletix.png?alt=media&token=a32e429b-4ece-45d2-bf00-85a8f9081a9c&_gl=1*14iryjj*_ga*MTcyOTkyMjIwMS4xNjk2NDU2NzU2*_ga_CW55HF8NVT*MTY5ODAxNDc6Mi4yLjEuMTY5ODAxNDg0Ny40OC4wLjA.' alt='Logo' class='logo'/>
                                <div class='company-details'>
                                    <strong>AthletiX</strong><br>
                                    La Molina, Av. la Fontana 1250, Lima 15024<br>
                                    Teléfono: +51 927572267<br>
                                    Email: jesus_soria@usmp.pe
                                </div>
                            </header>
                            <div class='client-info'>
                                <strong>Información del Cliente:</strong><br>
                                <strong>Nombre:</strong> {cliente.Nombres} {cliente.Apellidos}<br>
                                <strong>Email:</strong> {cliente.Email}<br>
                            </div>
                            <div class='invoice-details'>
                                <strong>Factura N°: {id}</strong><br>
                                Fecha: {DateTime.Now.ToString("dd/MM/yyyy")}<br>
                                <strong>Estado del Pedido:</strong> {pedido.Status}
                            </div>
                            <table>
                                <tr>
                                    <th>Producto</th>
                                    <th>Descripción</th>
                                    <th>Cantidad</th>
                                    <th>Precio Unitario (S/)</th>
                                    <th>Importe (S/)</th>
                                </tr>";

                                    foreach (var detalle in detalles)
                                    {
                                        html += $@"
                                        <tr>
                                            <td>{detalle.NombreProducto}</td>
                                            <td>{detalle.DescripcionProducto}</td>
                                            <td>{detalle.Cantidad}</td>
                                            <td>S/ {detalle.PrecioUnitario}</td>
                                            <td>S/ {detalle.Importe}</td>
                                        </tr>";
                                    }

                                    double subtotal = detalles.Sum(d => d.Importe);
                                    double impuesto = 0; // Aquí puedes calcular el impuesto si lo tienes.
                                    double descuento = 0; // Aquí puedes calcular el descuento si lo tienes.
                                    double total = subtotal + impuesto - descuento;

                                    html += $@"
                                    <tr>
                                        <td colspan='4' style='text-align:right;'><strong>Subtotal:</strong></td>
                                        <td>S/ {subtotal}</td>
                                    </tr>
                                    <tr>
                                        <td colspan='4' style='text-align:right;'><strong>Impuesto:</strong></td>
                                        <td>S/ {impuesto}</td>
                                    </tr>
                                    <tr>
                                        <td colspan='4' style='text-align:right;'><strong>Descuento:</strong></td>
                                        <td>S/ {descuento}</td>
                                    </tr>
                                    <tr class='highlighted'>
                                        <td colspan='4' style='text-align:right;'><strong>Total:</strong></td>
                                        <td >S/ {total}</td>
                                    </tr>
                            </table>
                            <div class='footer'>
                                Gracias por su compra.
                            </div>
                        </body>
                    </html>";

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                };
                var objectSettings = new ObjectSettings
                {
                    HtmlContent = html
                };
                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };
                var file = _converter.Convert(pdf);
                return File(file, "application/pdf", $"Pedido_{id}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al exportar el pedido {id} a PDF");
                return StatusCode(500, $"Ocurrió un error al exportar el pedido {id} a PDF. Por favor, inténtelo de nuevo más tarde.");
            }
        }




        /* Para exportar individualmente los Pedidos en excel */

        public async Task<ActionResult> ExportarUnSoloPedidoEnExcel(int? id)
        {
            try
            {
                if (id == null)
                {
                    return NotFound($"El pedido con ID {id} no fue encontrado, por eso no se puede exportar en Excel.");
                }

                Pedido? pedido = await _context.DataPedido.FindAsync(id);

                if (pedido == null)
                {
                    return NotFound($"El pedido con ID {id} no fue encontrado, por eso no se puede exportar en Excel.");
                }

                ApplicationUser? cliente = await _context.Users.FirstOrDefaultAsync(u => u.UserName == pedido.UserID);

                if (cliente == null)
                {
                    return NotFound($"El cliente con ID {pedido.UserID} no fue encontrado en la tabla de Clientes.");
                }

                var detalles = (from detalle in _context.DataDetallePedido
                                join producto in _context.Producto on detalle.Producto.id equals producto.id
                                where detalle.pedido.ID == pedido.ID
                                select new
                                {
                                    Cantidad = detalle.Cantidad,
                                    PrecioUnitario = detalle.Precio,
                                    NombreProducto = producto.Nombre,
                                    DescripcionProducto = producto.Descripcion,
                                    Importe = detalle.Cantidad * detalle.Precio
                                }).ToList();

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Pedido");

                // Estilos personalizados
                var titleStyle = package.Workbook.Styles.CreateNamedStyle("TitleStyle");
                titleStyle.Style.Font.Size = 28; // Aumentado para que se vea proporcional al logo
                titleStyle.Style.Font.Bold = true;
                titleStyle.Style.Font.Color.SetColor(System.Drawing.Color.DarkBlue); // Cambiar el color de la fuente a azul oscuro
                titleStyle.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid; // Establecer un fondo sólido

                // prueba para ver el fondo del titulo y logo del archivo

                // 1.- titleStyle.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray); // Color de fondo claro
                titleStyle.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightSkyBlue);  // Color de fondo Azul Suave
                // titleStyle.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.MintCream);  // Color de fondo Verde Menta
                // titleStyle.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Lavender);  // Color de fondo Lavanda
                // titleStyle.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.MistyRose);  // Color de fondo Melocotón
                // titleStyle.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Gainsboro);  // Color de fondo Gris Neutro



                titleStyle.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center; // Centrar horizontalmente
                titleStyle.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center; // Centrar verticalmente

                // <summary>

                var titleStyle2 = package.Workbook.Styles.CreateNamedStyle("TitleStyle2");
                titleStyle2.Style.Font.Size = 24; // Aumentado para que se vea proporcional al logo
                titleStyle2.Style.Font.Bold = true;

                // otro var de estilos de pruebas

                // otro var
                var headerStyle = package.Workbook.Styles.CreateNamedStyle("HeaderStyle");
                headerStyle.Style.Font.Bold = true;
                headerStyle.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerStyle.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightSkyBlue);
                headerStyle.Style.Font.Color.SetColor(System.Drawing.Color.DarkBlue);
                headerStyle.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center; // Centrar horizontalmente
                headerStyle.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center; // Centrar verticalmente

                // para ponerle bordes a los encabezados de productos descripcion, etc
                headerStyle.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                headerStyle.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                headerStyle.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                headerStyle.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                headerStyle.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Estilo para los bordes de las celdas
                var borderStyle = package.Workbook.Styles.CreateNamedStyle("BorderStyle");
                borderStyle.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderStyle.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderStyle.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderStyle.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderStyle.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Información de la Empresa
                worksheet.Cells[1, 1].Value = "AthletiX";
                worksheet.Cells[1, 1].StyleName = "TitleStyle";
                worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center; // Alinear verticalmente en el centro
                worksheet.Cells["A1:E1"].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, System.Drawing.Color.Black);
                worksheet.Row(1).Height = 80; // Ajusta el tamaño de la fila para el logo y el título

                // Descargar la imagen del logo
                using var client = new HttpClient();
                var logoBytes = await client.GetByteArrayAsync("https://firebasestorage.googleapis.com/v0/b/proyectos-cb445.appspot.com/o/img_logo_athletix.png?alt=media&token=a32e429b-4ece-45d2-bf00-85a8f9081a9c&_gl=1*14iryjj*_ga*MTcyOTkyMjIwMS4xNjk2NDU2NzU2*_ga_CW55HF8NVT*MTY5ODAxNDc6Mi4yLjEuMTY5ODAxNDg0Ny40OC4wLjA.");

                // Agregar la imagen al archivo Excel
                var image = worksheet.Drawings.AddPicture("Logo", new MemoryStream(logoBytes));
                image.SetPosition(0, 15, 3, 0); // Coloca el logo en la primera fila, columna E
                image.SetSize(100, 100);

                // Continuar con el resto de la información
                worksheet.Cells[2, 1].Value = "La Molina, Av. la Fontana 1250, Lima 15024";
                worksheet.Cells[3, 1].Value = "Teléfono: +51 927572267";
                worksheet.Cells[4, 1].Value = "Email: jesus_soria@usmp.pe";

                // Información del Cliente
                worksheet.Cells[6, 1].Value = "Información del Cliente:";
                worksheet.Cells[6, 1].StyleName = "titleStyle2";
                worksheet.Cells[7, 1].Value = $"Nombre: {cliente.Nombres} {cliente.Apellidos}";
                worksheet.Cells[8, 1].Value = $"Email: {cliente.Email}";

                // Detalles del Pedido
                worksheet.Cells[10, 1].Value = $"Factura N°: {id}";
                worksheet.Cells[11, 1].Value = $"Fecha: {DateTime.Now.ToString("dd/MM/yyyy")}";
                worksheet.Cells[12, 1].Value = $"Estado del Pedido: {pedido.Status}";

                // Encabezados de la tabla de productos
                string[] encabezados = { "Producto", "Descripción", "Cantidad", "Precio Unitario (S/)", "Importe (S/)" };
                for (int i = 0; i < encabezados.Length; i++)
                {
                    worksheet.Cells[14, i + 1].Value = encabezados[i];
                    worksheet.Cells[14, i + 1].StyleName = "HeaderStyle";
                }

                worksheet.Row(14).Height = 20; // Ajusta el tamaño de la fila para el logo y el título

                // Combinar celdas para títulos
                worksheet.Cells["A1:E1"].Merge = true;
                worksheet.Cells["A6:E6"].Merge = true;


                // Llenar la tabla con los detalles
                int filaInicio = 15;
                foreach (var detalle in detalles)
                {
                    worksheet.Cells[filaInicio, 1].Value = detalle.NombreProducto;
                    worksheet.Cells[filaInicio, 2].Value = detalle.DescripcionProducto;
                    worksheet.Cells[filaInicio, 3].Value = detalle.Cantidad;
                    worksheet.Cells[filaInicio, 4].Value = detalle.PrecioUnitario;
                    worksheet.Cells[filaInicio, 5].Value = detalle.Importe;

                    // Colores alternos en filas
                    if (filaInicio % 2 == 0)
                    {
                        worksheet.Cells[filaInicio, 1, filaInicio, 5].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[filaInicio, 1, filaInicio, 5].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(211, 211, 211)); // LightGray en ARGB
                    }

                    worksheet.Cells[filaInicio, 1, filaInicio, 5].StyleName = "BorderStyle"; // Aplicar bordes a las celdas de datos

                    filaInicio++;
                }

                // Totales
                worksheet.Cells[filaInicio + 1, 4].Value = "Subtotal:";
                worksheet.Cells[filaInicio + 1, 5].Value = detalles.Sum(d => d.Importe);
                worksheet.Cells[filaInicio + 2, 4].Value = "Total:";
                worksheet.Cells[filaInicio + 2, 5].Value = detalles.Sum(d => d.Importe); // Ajusta esto si agregas impuestos y descuentos

                // Aplicar el estilo headerStyle a las celdas "Subtotal" y "Total"
                worksheet.Cells[filaInicio + 1, 4].StyleName = "HeaderStyle";
                worksheet.Cells[filaInicio + 2, 4].StyleName = "HeaderStyle";

                // Aplicar bordes a las celdas "Subtotal", "Total" y sus montos
                worksheet.Cells[filaInicio + 1, 4, filaInicio + 2, 5].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[filaInicio + 1, 4, filaInicio + 2, 5].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[filaInicio + 1, 4, filaInicio + 2, 5].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[filaInicio + 1, 4, filaInicio + 2, 5].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                // Asegurarse de que los montos estén en negrita
                worksheet.Cells[filaInicio + 1, 5, filaInicio + 2, 5].Style.Font.Bold = true;

                // Formato de números
                worksheet.Cells[15, 3, filaInicio, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[15, 4, filaInicio + 2, 5].Style.Numberformat.Format = "#,##0.00";

                // Ajustar el ancho de las columnas automáticamente
                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Pedido_{id}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al exportar el pedido {id} a Excel");
                return StatusCode(500, $"Ocurrió un error al exportar el pedido {id} a Excel. Por favor, inténtelo de nuevo más tarde.");
            }
        }


        /* Hasta aqui son los metodos para exportar */






        /* metodo para buscar PEDIDO */
        /// <summary>
        /// Busca pedidos basados en el nombre de usuario(segun correo) y/o el estado del pedido. 
        /// Utiliza LINQ para filtrar los resultados en la base de datos. 
        /// Si no se encuentran coincidencias, muestra un mensaje al usuario. 
        /// Esta función me permite practicar cómo realizar búsquedas y filtrados 
        /// en ASP.NET Core y cómo manejar múltiples parámetros opcionales en los controladores.
        /// </summary>
        public async Task<IActionResult> BuscarPedido(string? searchUsername, string? orderStatus)
        {
            // Declara la variable pedidosPagedList una sola vez aquí
            IPagedList<Pedido> pedidosPagedList;

            try
            {
                var pedidos = from o in _context.DataPedido select o;

                if (!String.IsNullOrEmpty(searchUsername) && !String.IsNullOrEmpty(orderStatus))
                {
                    pedidos = pedidos.Where(s => s.UserID.Contains(searchUsername) && s.Status.Contains(orderStatus));
                }
                else if (!String.IsNullOrEmpty(searchUsername))
                {
                    pedidos = pedidos.Where(s => s.UserID.Contains(searchUsername));
                }
                else if (!String.IsNullOrEmpty(orderStatus))
                {
                    pedidos = pedidos.Where(s => s.Status.Contains(orderStatus));
                }

                var pedidosList = await pedidos.ToListAsync();

                if (!pedidosList.Any())
                {
                    TempData["MessageDeRespuesta"] = "No se encontraron pedidos que coincidan con la búsqueda.";
                    pedidosPagedList = new PagedList<Pedido>(new List<Pedido>(), 1, 1);
                }
                else
                {
                    pedidosPagedList = pedidosList.ToPagedList(1, pedidosList.Count);
                }
            }
            catch (Exception ex)
            {
                TempData["MessageDeRespuesta"] = "Ocurrió un error al buscar pedidos. Por favor, inténtalo de nuevo más tarde.";
                pedidosPagedList = new PagedList<Pedido>(new List<Pedido>(), 1, 1);
            }

            // Retorna la vista con pedidosPagedList, que siempre tendrá un valor asignado.
            return View("ListaDePedidos", pedidosPagedList);
        }

        /// <summary>
        /// Busca un pedido específico por su ID para editar. 
        /// Si el ID no se proporciona o el pedido no se encuentra, 
        /// retorna un error de "No Encontrado". 
        /// Esta función es crucial para entender cómo manejar consultas 
        /// a la base de datos y errores comunes en una aplicación web ASP.NET Core.
        /// </summary>
        public async Task<ActionResult> EditarPedido(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Pedido? pedido = await _context.DataPedido.FindAsync(id);

            if (pedido == null)
            {
                return NotFound();
            }

            return View("EditarPedido", pedido);
        }

        /// <summary>
        /// Actualiza el estado de un pedido específico en la base de datos. 
        /// Primero verifica si el pedido existe, y luego actualiza solo el estado del pedido. 
        /// Si la actualización es exitosa, redirige al usuario de nuevo a la página de edición con un mensaje de éxito. 
        /// En caso de error, muestra un mensaje de error y vuelve a la vista de edición. 
        /// Esta función me ayuda a comprender cómo manejar actualizaciones parciales en ASP.NET Core y cómo gestionar errores en operaciones de base de datos.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> GuardarPedidoEditado(int id, Pedido pedidoActualizado)
        {
            // Validar si el ID del pedido es válido
            Pedido? pedido = await _context.DataPedido.FindAsync(id);

            if (pedido == null)
            {
                return NotFound();
            }

            // Actualizar solo el estado del pedido
            pedido.Status = pedidoActualizado.Status;

            try
            {
                _context.DataPedido.Update(pedido);
                await _context.SaveChangesAsync();
                TempData["MessageActualizandoPedido"] = "Estado del pedido actualizado exitosamente.";
                return RedirectToAction("EditarPedido", new { id = pedido.ID });
            }
            catch (Exception ex)
            {
                // Aquí puedes manejar cualquier error que pueda surgir al intentar actualizar el pedido en la base de datos.
                TempData["ErrorActualizandoPedido"] = "Ocurrió un error al actualizar el estado del pedido. Por favor, inténtalo de nuevo.";
                return View("EditarPedido", pedido);
            }
        }

        /// <summary>
        /// Obtiene y muestra los detalles de un pedido específico basado en su ID. 
        /// Recupera el pedido y sus detalles asociados, y luego construye un modelo de vista 
        /// que combina la información del pedido y sus productos relacionados. 
        /// Si el pedido no se encuentra, muestra una página de error. 
        /// Esta función es esencial para entender cómo realizar consultas relacionadas 
        /// y cómo construir modelos de vista complejos en ASP.NET Core.
        /// </summary>
        public async Task<IActionResult> VerPedido(int? id)
        {
            try
            {
                var pedido = await _context.DataPedido.FirstOrDefaultAsync(p => p.ID == id);

                if (pedido == null)
                {
                    return View("Error", new { message = "Pedido no encontrado." });
                }

                var detalles = (from detalle in _context.DataDetallePedido
                                join producto in _context.Producto on detalle.Producto.id equals producto.id
                                where detalle.pedido.ID == pedido.ID
                                select new DetallePedidoViewModel
                                {
                                    Cantidad = detalle.Cantidad,
                                    PrecioUnitario = detalle.Precio,
                                    NombreProducto = producto.Nombre,
                                    DescripcionProducto = producto.Descripcion,
                                    ImagenProducto = producto.Imagen,
                                    // SE PUEDE AGREGAR MAS CAMPOS DE LA TABLA PRODUCTO SI ASI LO QUIERES, ESTO PARA MI ES NECESARIO
                                }).ToList();

                var viewModel = new PedidoViewModel
                {
                    ID = pedido.ID,
                    Status = pedido.Status,
                    Items = detalles,
                    Total = pedido.Total
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Un error inesperado ocurrió mientras se obtenían los detalles del pedido.");
                return View("Error");
            }
        }



        /* query en base de datos oracle para probar una teoria, esta era la idea original solo mostrar los 4 ultimos numeros de la tarjeta pero luego pense mejor muestro toda xd
        SELECT 
            p.id AS "ID Pedido",
            p."UserID",
            p."Total",
            p."Status",
            pa."PaymentDate" AS "Fecha de Pago",
            pa."NombreTarjeta",
            RIGHT(pa."NumeroTarjeta", 4) AS "Últimos 4 dígitos Tarjeta",
            pa."MontoTotal" AS "Monto Pagado",
            d."Productoid" AS "ID Producto",
            d."Cantidad",
            d."Precio" AS "Precio Unitario"
        FROM 
            "t_order" p
        INNER JOIN 
            "t_order_detail" d ON p.id = d."pedidoID"
        INNER JOIN 
            "t_pago" pa ON p."pagoId" = pa.id;
        */

    }
}