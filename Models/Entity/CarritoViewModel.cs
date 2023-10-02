using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace proyecto_ecommerce_deportivo_net.Models.Entity
{
    public class CarritoViewModel
    {
        public List<Proforma> Items { get; set; }
        public double Subtotal { get; set; }
        public double Descuento { get; set; }
        public double Total { get; set; }
    }
}