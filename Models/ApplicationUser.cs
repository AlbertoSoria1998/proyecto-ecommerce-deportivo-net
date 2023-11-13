using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
namespace proyecto_ecommerce_deportivo_net.Models {
    public class ApplicationUser : IdentityUser {

        //[Required] // Esto asegura que el campo no puede ser null
        [PersonalData]
        public string? Nombres { get; set; }

        //[Required] // Esto asegura que el campo no puede ser null
        [PersonalData]
        public string? Apellidos { get; set; }

        //[Required] // Esto asegura que el campo no puede ser null
        [PersonalData]
        public string? Dni { get; set; }

        public string Rol { get; set; } = "Cliente"; // Atributo nuevo para el rol, por defecto es "Cliente"
    }
    
}