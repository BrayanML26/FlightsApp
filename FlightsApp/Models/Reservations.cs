using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightsApp.Models
{
    public class Reservations
    {
        public int ReservationId { get; set; }
        public int FlightId { get; set; }
        public string? PassengerName { get; set; }
        public string? PassengerEmail { get; set; }
    }
}
