using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class testcontroller : ControllerBase
    {
        private readonly DataContext _context;

        public testcontroller(DataContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Add(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Ok(product);
        }

    }
}
