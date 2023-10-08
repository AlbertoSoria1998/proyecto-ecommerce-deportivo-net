using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using proyecto_ecommerce_deportivo_net.Data;
using proyecto_ecommerce_deportivo_net.Models;

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
using System.IO;
using System.Linq;
using OfficeOpenXml.Table;
using proyecto_ecommerce_deportivo_net.Models;
using proyecto_ecommerce_deportivo_net.Models.Entity;
using Microsoft.AspNetCore.Identity;

namespace proyecto_ecommerce_deportivo_net.Controllers
{

    public class MisPedidosController : Controller
    {
        private readonly ILogger<MisPedidosController> _logger;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        // Objeto para la exportación
        private readonly IConverter _converter;
        public MisPedidosController(ILogger<MisPedidosController> logger, UserManager<ApplicationUser> userManager, ApplicationDbContext context, IConverter converter)
        {
            _logger = logger;

            _userManager = userManager;
            _context = context;

            ModelState.Clear();


            _converter = converter; // PARA EXPORTAR
        }

        public async Task<IActionResult> MisPedidos(int? page)
        {
            var userId = _userManager.GetUserName(User); //sesion

            if (userId == null)
            {
                // no se ha logueado
                TempData["MessageLOGUEARSE"] = "Por favor debe loguearse antes de agregar un producto";
                return View("~/Views/Home/Index.cshtml");
            }
            else
            {
                int pageNumber = (page ?? 1); // Si no se especifica la página, asume la página 1
                int pageSize = 3; // Máximo 3 pedidos por página

                pageNumber = Math.Max(pageNumber, 1); // Con esto se asegura de que pageNumber nunca sea menor que 1

                var pedidosDelCliente = _context.DataPedido.Where(p => p.UserID == userId);

                // Aquí aplicamos la paginación.
                var listaPaginada = await pedidosDelCliente.ToPagedListAsync(pageNumber, pageSize);

                return View("MisPedidos", listaPaginada);
            }
        }

        /* metodos para exportar en pdf y excel desde aqui para abajo */
        public IActionResult ExportarPedidosEnPDF()
        {
            try
            {
                var userId = _userManager.GetUserName(User); //sesion

                if (userId == null)
                {
                    // no se ha logueado
                    TempData["MessageLOGUEARSE"] = "Por favor debe loguearse antes de exportar";
                    return View("~/Views/Home/Index.cshtml");
                }
                else
                {
                    // Filtrar por el ID del usuario logueado en este caso el id es el email
                    var pedidos = _context.DataPedido.Where(p => p.UserID == userId).ToList();
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
                    <img src='https://firebasestorage.googleapis.com/v0/b/proyectos-cb445.appspot.com/o/logo.png?alt=media&token=b4dc8219-9bbd-4101-918f-153bc4bb87e8&_gl=1*1eklxby*_ga*MTcyOTkyMjIwMS4xNjk2NDU2NzU2*_ga_CW55HF8NVT*MTY5NjQ1Njc1NS4xLjEuMTY5NjQ1NzY1NS4yLjAuMA..' alt='Logo' width='100' class='logo'/>
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
                var userId = _userManager.GetUserName(User); // Obtener el ID del usuario logueado, el email

                if (userId == null)
                {
                    // No se ha logueado
                    TempData["MessageLOGUEARSE"] = "Por favor debe loguearse antes de exportar";
                    return View("~/Views/Home/Index.cshtml");
                }
                else
                {
                    var resultados = (from p in _context.DataPedido
                                      where p.UserID == userId  // Filtrar por el ID del usuario logueado en este caso el id es el email
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

            }
            catch (Exception ex)
            {
                // Loguear el error para obtener más detalles
                _logger.LogError(ex, "Error al exportar pedidos a Excel");
                // Retornar un mensaje de error al usuario
                return StatusCode(500, "Ocurrió un error al exportar los pedidos a Excel. Por favor, inténtelo de nuevo más tarde.");
            }
        }



        /* Para exportar individualmente los Pedidos */
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

                var html = $@"
            <html>
                <head>
                <meta charset='UTF-8'>
                    <style>
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
                        img.logo {{
                            position: absolute;
                            top: 0;
                            right: 0;
                            border-radius:50%;
                            height:3.3rem;
                            width:3.3rem;
                        }}

                        h1 {{
                            color: #40E0D0; /* Color celeste */
                        }}
                    </style>
                </head>
                <body>
                    <img src='https://firebasestorage.googleapis.com/v0/b/proyectos-cb445.appspot.com/o/logo.png?alt=media&token=b4dc8219-9bbd-4101-918f-153bc4bb87e8&_gl=1*1eklxby*_ga*MTcyOTkyMjIwMS4xNjk2NDU2NzU2*_ga_CW55HF8NVT*MTY5NjQ1Njc1NS4xLjEuMTY5NjQ1NzY1NS4yLjAuMA..' alt='Logo' width='100' class='logo'/>
                    <h1>Reporte de Pedido {id}</h1>
                    <table>
                        <tr>
                            <th>ID</th>
                            <th>UserID</th>
                            <th>Total (en soles)</th>
                      
                            <th>Status</th>
                        </tr>";




                html += $@"
                <tr>
                    <td>{pedido.ID}</td>
                    <td>{pedido.UserID}</td>
                    <td>{pedido.Total}</td>
               
                    <td>{pedido.Status}</td>
           
                </tr>";


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
                // Loguear el error para obtener más detalles
                _logger.LogError(ex, $"Error al exportar el pedido {id} a PDF");
                // Retornar un mensaje de error al usuario
                return StatusCode(500, $"Ocurrió un error al exportar el pedido {id} a PDF. Por favor, inténtelo de nuevo más tarde.");
            }
        }



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



                var resultados = (from p in _context.DataPedido
                                  where p.ID == id  // Filtrar por ID del pedido, esto es lo unico diferente al metdo de exportar todos en excel
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
                var worksheet = package.Workbook.Worksheets.Add("Pedido");

                // Agregando un título arriba de la tabla
                worksheet.Cells[1, 1].Value = $"Reporte del Pedido {id}";
                worksheet.Cells[1, 1].Style.Font.Size = 20;
                worksheet.Cells[1, 1].Style.Font.Bold = true;

                // Cargar los datos en la fila 3 para dejar espacio para el título de Reporte de Pedidos
                worksheet.Cells[3, 1].LoadFromCollection(resultados, true);

                // Dar formato a la tabla Reporte de Pedidos
                var dataRange = worksheet.Cells[2, 1, worksheet.Dimension.End.Row, worksheet.Dimension.End.Column];
                var table = worksheet.Tables.Add(dataRange, "Pedido");
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

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Pedido_{id}.xlsx");
            }
            catch (Exception ex)
            {
                // Loguear el error para obtener más detalles
                _logger.LogError(ex, $"Error al exportar el pedido {id} a Excel");
                // Retornar un mensaje de error al usuario
                return StatusCode(500, $"Ocurrió un error al exportar el pedido {id} a Excel. Por favor, inténtelo de nuevo más tarde.");
            }
        }

        /* Hasta aqui son los metodos para exportar */

        /* metodo para buscar PEDIDO */

        public async Task<IActionResult> BuscarPedido(int? searchPedidoID, string? orderStatus)
        {
            // Declara la variable pedidosPagedList una sola vez aquí
            IPagedList<Pedido> pedidosPagedList;

            // Obtener el UserID del cliente logueado
            var userId = _userManager.GetUserName(User);

            if (userId == null)
            {
                // No se ha logueado
                TempData["MessageLOGUEARSE"] = "Por favor debe loguearse antes de buscar pedidos.";
                return View("~/Views/Home/Index.cshtml");
            }

            try
            {
                var pedidos = from o in _context.DataPedido where o.UserID == userId select o;

                if (searchPedidoID.HasValue && !String.IsNullOrEmpty(orderStatus))
                {
                    pedidos = pedidos.Where(s => s.ID == searchPedidoID.Value && s.Status.Contains(orderStatus));
                }
                else if (searchPedidoID.HasValue)
                {
                    pedidos = pedidos.Where(s => s.ID == searchPedidoID.Value);
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
            return View("MisPedidos", pedidosPagedList);
        }

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





        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}