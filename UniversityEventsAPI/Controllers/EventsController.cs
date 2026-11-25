using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using UniversityEventsAPI.Models;

namespace UniversityEventsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        // Controller hər sorğuda yenidən yaradıldığı üçün siyahı static olmalıdır
        private static readonly List<Event> _events = new();
        private static int _nextId = 1;
        private static readonly object _lock = new();

        // ---------- Yoxlama (validation) ----------
        private static string? Validate(Event e)
        {
            if (string.IsNullOrWhiteSpace(e.Name))
                return "Tədbirin adı boş ola bilməz.";

            if (e.EventDate < DateTime.Now)
                return "Tədbirin tarixi keçmiş tarix ola bilməz.";

            if (e.MaxParticipants < 1)
                return "İştirakçı sayı 1-dən az ola bilməz.";

            return null; // xəta yoxdur
        }

        // ---------- GET /api/events ----------
        // Əlavə tapşırıqlar query parametrləri ilə:
        // ?search=ai  ?city=Baku  ?upcomingOnly=true  ?minParticipants=100  ?sortByDate=asc|desc
        [HttpGet]
        public ActionResult<IEnumerable<Event>> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? city,
            [FromQuery] bool upcomingOnly = false,
            [FromQuery] int? minParticipants = null,
            [FromQuery] string? sortByDate = null)
        {
            IEnumerable<Event> query;
            lock (_lock) { query = _events.ToList(); }

            // 1. Adına görə axtarış
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

            // 4. Şəhərə görə filtr (Location sahəsi üzrə)
            if (!string.IsNullOrWhiteSpace(city))
                query = query.Where(e => e.Location.Equals(city, StringComparison.OrdinalIgnoreCase));

            // 3. Yalnız gələcək tədbirlər
            if (upcomingOnly)
                query = query.Where(e => e.EventDate > DateTime.Now);

            // 5. İştirakçı sayı verilən dəyərdən çox olanlar (məs. minParticipants=100)
            if (minParticipants.HasValue)
                query = query.Where(e => e.MaxParticipants > minParticipants.Value);

            // 2. Tarixə görə sıralama
            if (sortByDate?.ToLower() == "desc")
                query = query.OrderByDescending(e => e.EventDate);
            else if (sortByDate?.ToLower() == "asc")
                query = query.OrderBy(e => e.EventDate);

            return Ok(query.ToList());
        }

        // ---------- 6. GET /api/events/export ----------
        [HttpGet("export")]
        public IActionResult Export()
        {
            List<Event> copy;
            lock (_lock) { copy = _events.ToList(); }

            var json = JsonSerializer.SerializeToUtf8Bytes(copy,
                new JsonSerializerOptions { WriteIndented = true });

            return File(json, "application/json", "events.json");
        }

        // ---------- GET /api/events/{id} ----------
        [HttpGet("{id:int}")]
        public ActionResult<Event> GetById(int id)
        {
            Event? ev;
            lock (_lock) { ev = _events.FirstOrDefault(e => e.Id == id); }

            if (ev == null)
                return NotFound($"ID = {id} olan tədbir tapılmadı.");

            return Ok(ev);
        }

        // ---------- POST /api/events ----------
        [HttpPost]
        public ActionResult<Event> Create([FromBody] Event newEvent)
        {
            var error = Validate(newEvent);
            if (error != null)
                return BadRequest(error);

            lock (_lock)
            {
                newEvent.Id = _nextId++;
                _events.Add(newEvent);
            }

            // 201 Created + Location header
            return CreatedAtAction(nameof(GetById), new { id = newEvent.Id }, newEvent);
        }

        // ---------- PUT /api/events/{id} ----------
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] Event updated)
        {
            var error = Validate(updated);
            if (error != null)
                return BadRequest(error);

            lock (_lock)
            {
                var existing = _events.FirstOrDefault(e => e.Id == id);
                if (existing == null)
                    return NotFound($"ID = {id} olan tədbir tapılmadı.");

                existing.Name = updated.Name;
                existing.Description = updated.Description;
                existing.Location = updated.Location;
                existing.EventDate = updated.EventDate;
                existing.MaxParticipants = updated.MaxParticipants;

                return Ok(existing);
            }
        }

        // ---------- DELETE /api/events/{id} ----------
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            lock (_lock)
            {
                var existing = _events.FirstOrDefault(e => e.Id == id);
                if (existing == null)
                    return NotFound($"ID = {id} olan tədbir tapılmadı.");

                _events.Remove(existing);
            }

            return NoContent(); // 204
        }
    }
}
