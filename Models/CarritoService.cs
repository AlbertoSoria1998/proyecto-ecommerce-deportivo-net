using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using proyecto_ecommerce_deportivo_net.Data;
using proyecto_ecommerce_deportivo_net.Models.Entity;

namespace proyecto_ecommerce_deportivo_net.Models
{
    public class CarritoService: ICarritoService
    {
        private readonly ApplicationDbContext _context;

        public CarritoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Proforma>> ObtenerItems(string userId)
        {
            return await _context.DataCarrito
                .Where(p => p.UserID == userId && p.Status == "PENDIENTE")
                .Include(p => p.Producto)
                .ToListAsync();
        }

        public async Task<bool> ActualizarCantidad(int id, int cantidad, string userId)
        {
            var item = await _context.DataCarrito.FindAsync(id);
            if (item != null && item.UserID == userId)
            {
                item.Cantidad = cantidad;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<double> ObtenerSubtotal(string userId)
        {
            var items = await ObtenerItems(userId);
            return items.Sum(item => item.Precio * item.Cantidad);
        }

        public async Task<double> ObtenerDescuento(string userId)
        {
            var subtotal = await ObtenerSubtotal(userId);
            return CalcularDescuento(subtotal);
        }

        public async Task<double> ObtenerTotal(string userId)
        {
            var subtotal = await ObtenerSubtotal(userId);
            var descuento = await ObtenerDescuento(userId);
            return subtotal - descuento;
        }

        private double CalcularDescuento(double subtotal)
        {
            return subtotal * 0.10;
        }

        public async Task<bool> QuitarDelCarrito(int id, string userId)
        {
            var item = await _context.DataCarrito.FindAsync(id);
            if (item != null && item.UserID == userId)
            {
                _context.DataCarrito.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
    }
}