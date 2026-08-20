using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class ClienteService : IClienteService
    {
        private readonly PanaderiaContext _context;

        public ClienteService(PanaderiaContext context)
        {
            _context = context;
        }

        //listado de clientes
        public async Task<IEnumerable<Cliente>> GetAllAsync()
        {
            return await _context.Clientes
                .OrderBy(c => c.Nombre)
                .ThenBy(c => c.Apellido)
                .ToListAsync();
        }

        //obtener un cliente por su ID
        public async Task<Cliente?> GetByIdAsync(int id)
        {
            return await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
        }

        //crear un nuevo cliente
        public async Task CreateAsync(Cliente cliente)
        {
            await _context.Clientes.AddAsync(cliente);
            await _context.SaveChangesAsync();
        }

        //actualizar un cliente existente
        public async Task UpdateAsync(Cliente cliente)
        {
            var existe = await _context.Clientes.FindAsync(cliente.Id);
            if (existe == null) return;

            existe.Nombre = cliente.Nombre;
            existe.Apellido = cliente.Apellido;
            existe.Direccion = cliente.Direccion;
            existe.Localidad = cliente.Localidad;
            existe.Provincia = cliente.Provincia;
            existe.Telefono = cliente.Telefono;
            existe.Revendedor = cliente.Revendedor;
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //eliminar un cliente por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.Clientes.Where(c => c.Id == id).ExecuteDeleteAsync();
        }

        //verificar si un cliente existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Clientes.AnyAsync(c => c.Id == id);
        }

        // Cantidad de dígitos finales que se comparan para identificar un teléfono.
        // Con los últimos 6 el mismo número coincide se haya cargado con o sin característica,
        // con o sin el 15, con +54, con espacios o con guiones.
        private const int DigitosComparados = 6;

        //buscar un cliente por teléfono (compara los últimos dígitos, ignora formato)
        public async Task<Cliente?> GetByTelefonoAsync(string telefono)
        {
            var buscado = SoloDigitos(telefono);
            if (string.IsNullOrEmpty(buscado)) return null;

            var clientes = await _context.Clientes
                .Where(c => c.Telefono != null && c.Telefono != "")
                .ToListAsync();

            var candidatos = clientes
                .Select(c => new { Cliente = c, Digitos = SoloDigitos(c.Telefono!) })
                .Where(x => CoincidenTelefonos(x.Digitos, buscado))
                .ToList();

            if (candidatos.Count <= 1)
                return candidatos.FirstOrDefault()?.Cliente;

            // Si varios coinciden por los últimos 6 (misma terminación, distinta característica),
            // gana el que comparte más dígitos finales con el que se ingresó.
            return candidatos
                .OrderByDescending(x => LargoSufijoComun(x.Digitos, buscado))
                .ThenByDescending(x => x.Cliente.Id)
                .First()
                .Cliente;
        }

        private static bool CoincidenTelefonos(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return false;

            // Números cortos o mal cargados: exigimos coincidencia exacta para no unir clientes distintos
            if (a.Length < DigitosComparados || b.Length < DigitosComparados)
                return a == b;

            return a[^DigitosComparados..] == b[^DigitosComparados..];
        }

        private static int LargoSufijoComun(string a, string b)
        {
            var largo = 0;
            while (largo < a.Length && largo < b.Length && a[^(largo + 1)] == b[^(largo + 1)])
                largo++;
            return largo;
        }

        private static string SoloDigitos(string valor)
        {
            return new string(valor.Where(char.IsDigit).ToArray());
        }
    }
}