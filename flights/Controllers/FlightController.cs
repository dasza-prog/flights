﻿using flights.ReadModels;
using flights.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System;
using flights.DTOs;
using flights.Domain.Errors;
using flights.Data;
using Microsoft.EntityFrameworkCore;

namespace Flights.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FlightController : ControllerBase
    {
        private readonly ILogger<FlightController> _logger;
        
        private readonly Entities _entities;

        public FlightController(ILogger<FlightController> logger,
            Entities entities)
        {
            _logger = logger;
            _entities = entities;
        }


        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(typeof(IEnumerable<FlightRm>), 200)]
        [HttpGet]
        public IEnumerable<FlightRm> Search([FromQuery] FlightSearchParameters @params)
        {
            _logger.LogInformation("Searching for a flight for: {Destination}", @params.Destination);

            IQueryable<Flight> flights = _entities.Flights;

            if(!string.IsNullOrWhiteSpace(@params.Destination))
                flights = flights.Where(f => f.Arrival.Place.Contains(@params.Destination));

            if (!string.IsNullOrWhiteSpace(@params.From))
                flights = flights.Where(f => f.Departure.Place.Contains(@params.From));

            if (@params.FromDate != null)
                flights = flights.Where(f => f.Departure.Time >= (@params.FromDate.Value.Date));

            if (@params.ToDate != null)
                flights = flights.Where(f => f.Departure.Time >= (@params.ToDate.Value.Date.AddDays(1).AddTicks(-1)));

            if (@params.NumberOfPassengers != 0 && @params.NumberOfPassengers != null)
                flights = flights.Where(f => f.RemainingNumberOfSeats >= (@params.NumberOfPassengers));

            else
                flights = flights.Where(f => f.RemainingNumberOfSeats >= 1);

            var flightRmList = flights
                .Select(flight => new FlightRm(
                flight.Id,
                flight.Airline,
                flight.Price,
                new TimePlaceRm(flight.Departure.Place.ToString(), flight.Departure.Time),
                new TimePlaceRm(flight.Arrival.Place.ToString(), flight.Arrival.Time),
                flight.RemainingNumberOfSeats
                ));

            return flightRmList;
        }

        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(typeof(FlightRm), 200)]
        [HttpGet("{id}")]
        public ActionResult<FlightRm> Find(Guid id)
        {
            var flight = _entities.Flights.SingleOrDefault(f => f.Id == id);
            
            if(flight == null)
                return NotFound();

            var readModel = new FlightRm(
                flight.Id,
                flight.Airline,
                flight.Price,
                new TimePlaceRm(flight.Departure.Place.ToString(), flight.Departure.Time),
                new TimePlaceRm(flight.Arrival.Place.ToString(), flight.Arrival.Time),
                flight.RemainingNumberOfSeats
                );

            return Ok(readModel);

        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public IActionResult Book(BookDTO dto)
        {
            var flight = _entities.Flights.SingleOrDefault(f => f.Id == dto.FlightId);
            if(flight == null)
                return NotFound();

            var error = flight.MakeBooking(dto.PassengerEmail, dto.NumberOfSeats);
            if(error is OverbookError)
            {
                return Conflict(new {message="Not enough seats."});
            }

            try
            {
                _entities.SaveChanges();
            } catch(DbUpdateConcurrencyException e) 
            {
                return Conflict(new { message = "An error occurred while booking. Please try again." });

            }


            System.Diagnostics.Debug.WriteLine($"Booking a new flight {dto.FlightId}");
            return CreatedAtAction(nameof(Find), new {id = dto.FlightId });
        }

    }
}