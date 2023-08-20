using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using flights.Data;
using flights.ReadModels;
using flights.DTOs;
using flights.Domain.Errors;

namespace flights.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class BookingController : ControllerBase
    {
        private readonly Entities _entities;

        public BookingController(Entities entities)
        {
            _entities = entities;
        }

        [HttpGet("{email}")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(IEnumerable<BookingRm>), StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<BookingRm>> List(string email)
        {
            var bookings = _entities.Flights.ToArray()
                .SelectMany(f => f.Bookings
                    .Where(b => b.PassengerEmail == email)
                    .Select(b => new BookingRm(
                        f.Id,
                        f.Airline,
                        f.Price.ToString(),
                        new TimePlaceRm(f.Arrival.Place, f.Arrival.Time),
                        new TimePlaceRm(f.Departure.Place, f.Departure.Time),
                        b.NumberOfSeats,
                        email
                        )));

            return Ok(bookings);
        }


        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(IEnumerable<FlightRm>), 200)]
        [HttpDelete]
        public IActionResult Cancel(BookDTO dTO)
        {
            var flight = _entities.Flights.Find(dTO.FlightId);

            var error = flight?.CancelBooking(dTO.PassengerEmail, dTO.NumberOfSeats);

            if(error == null) 
            {
                _entities.SaveChanges();
                return NoContent();
            }

            if(error is NotFoundError)
                return NotFound();

            throw new Exception($"The error of type: {error.GetType().Name} occurred while canceling the booking made by {dTO.PassengerEmail}");

        }
    }
}
