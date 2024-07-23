using FlightsApp.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using Document = iText.Layout.Document;

namespace FlightAPP
{
    class Program
    {
        static string connectionString = "Data Source=;Initial Catalog=DB_FLIGHTS;Integrated Security=True";

        static async Task Main(string[] args)
        {
            // Ejecutar procesos en paralelo
            Task<List<Flights>> consultaDisponibilidadTask = Task.Run(() => ConsultarDisponibilidadVuelos());
            Task<Reservations> reservaVuelosTask = Task.Run(() => ReservarVuelos());
            Task actualizacionInventarioTask = Task.Run(() => ActualizarInventario());
            Task envioConfirmacionesTask = Task.Run(() => EnviarConfirmaciones());

            // Esperar a que todos los procesos se completen
            await Task.WhenAll(consultaDisponibilidadTask, reservaVuelosTask, actualizacionInventarioTask, envioConfirmacionesTask);

            Console.WriteLine("Procesos en paralelo iniciados. Presiona cualquier tecla para salir.");
            Console.ReadKey();
        }

        static List<Flights> ConsultarDisponibilidadVuelos()
        {
            // Consulta de disponibilidad de vuelos
            Console.WriteLine("Consultando disponibilidad de vuelos...");

            // Conexión a la base de datos 
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Consulta SQL para obtener los vuelos disponibles
                string query = "SELECT * FROM Flights WHERE SeatsAvailable > 0";

                // Ejecutar la consulta
                SqlCommand command = new SqlCommand(query, connection);
                SqlDataReader reader = command.ExecuteReader();

                // Lista para almacenar los vuelos disponibles
                List<Flights> vuelosDisponibles = new List<Flights>();

                // Procesar los resultados de la consulta
                while (reader.Read())
                {
                    Flights vuelo = new Flights
                    {
                        FlightId = Convert.ToInt32(reader["FlightId"]),
                        Departure = reader["Departure"].ToString(),
                        Destination = reader["Destination"].ToString(),
                        DepartureTime = Convert.ToDateTime(reader["DepartureTime"]),
                        ArrivalTime = Convert.ToDateTime(reader["ArrivalTime"]),
                        SeatsAvailable = Convert.ToInt32(reader["SeatsAvailable"])
                    };
                    vuelosDisponibles.Add(vuelo);
                }

                // Cerrar la conexión
                reader.Close();

                return vuelosDisponibles;
            }
        }

        static Reservations ReservarVuelos()
        {
            // Reserva de vuelos
            Console.WriteLine("Reservando vuelos...");

            // Datos de la reserva
            int flightId = 8; // ID del vuelo reservado 
            string passengerName = "example "; // Nombre del pasajero 
            string passengerEmail = "example@gmail.com"; // Correo electrónico del pasajero 

            // Conexión a la base de datos 
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string insertQuery = @"
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM Reservations 
                        WHERE FlightId = @FlightId 
                        AND PassengerName = @PassengerName
                        AND PassengerEmail = @PassengerEmail
                    ) 
                    BEGIN 
                        INSERT INTO Reservations (FlightId, PassengerName, PassengerEmail) 
                        VALUES (@FlightId, @PassengerName, @PassengerEmail); 
                        SELECT SCOPE_IDENTITY();
                    END";
                SqlCommand command = new SqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@FlightId", flightId);
                command.Parameters.AddWithValue("@PassengerName", passengerName);
                command.Parameters.AddWithValue("@PassengerEmail", passengerEmail);

                // Ejecutar la inserción de la reserva y obtener el ID de la reserva insertada
                int reservationId = Convert.ToInt32(command.ExecuteScalar());

                // Crear un objeto Reservation con los datos insertados
                Reservations reservation = new Reservations
                {
                    ReservationId = reservationId,
                    FlightId = flightId,
                    PassengerName = passengerName,
                    PassengerEmail = passengerEmail
                };

                // Retornar la reserva
                return reservation;
            }
        }

        static Flights ConsultarVueloPorId(int flightId)
        {
            // Consulta de vuelo por ID
            Console.WriteLine($"Consultando vuelo con ID {flightId}...");

            // Objeto para almacenar el vuelo encontrado
            Flights vuelo = null;

            // Conexión a la base de datos 
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Consulta SQL para obtener el vuelo por ID
                string query = "SELECT * FROM Flights WHERE FlightId = @FlightId";

                // Ejecutar la consulta
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@FlightId", flightId);
                SqlDataReader reader = command.ExecuteReader();

                // Verificar si se encontró un vuelo con el ID especificado
                if (reader.Read())
                {
                    vuelo = new Flights
                    {
                        FlightId = Convert.ToInt32(reader["FlightId"]),
                        Departure = reader["Departure"].ToString(),
                        Destination = reader["Destination"].ToString(),
                        DepartureTime = Convert.ToDateTime(reader["DepartureTime"]),
                        ArrivalTime = Convert.ToDateTime(reader["ArrivalTime"]),
                        SeatsAvailable = Convert.ToInt32(reader["SeatsAvailable"])
                    };
                }

                // Cerrar la conexión
                reader.Close();
            }

            // Retornar el vuelo encontrado
            return vuelo;
        }

        static void ActualizarInventario()
        {
            // Actualización de inventario de asientos disponibles
            Console.WriteLine("Actualizando inventario de asientos disponibles...");

            Reservations reservation = ReservarVuelos();
            int id = reservation.FlightId;

            // Conexión a la base de datos 
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string updateQuery = "UPDATE Flights SET SeatsAvailable = SeatsAvailable - 1 WHERE FlightId = @FlightId";
                SqlCommand command = new SqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@FlightId", id); // ID del vuelo reservado 
                command.ExecuteNonQuery();
            }
        }

        static void EnviarConfirmaciones()
        {
            // Envío de correos electrónicos de confirmación
            Console.WriteLine("Enviando confirmaciones de reserva por correo electrónico...");

            Reservations reservation = ReservarVuelos();
            int id = reservation.FlightId;
            Flights vuelos = ConsultarVueloPorId(id);

            string passengerName = reservation.PassengerName; // Obtener el nombre del pasajero
            string passengerEmail = reservation.PassengerEmail; // Obtener el correo electrónico del pasajero
            string flightDetails = $"Departure: {vuelos.Departure} - Destination: {vuelos.Destination} - DepartureTime: {vuelos.DepartureTime} - ArrivalTime: {vuelos.ArrivalTime}"; // Obtener los detalles del vuelo

            // Generar y adjuntar el documento PDF
            string pdfFilePath = GenerarPDF(passengerName, passengerEmail, flightDetails);

            try
            {
                // Configuración del cliente SMTP
                SmtpClient smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("example@gmail.com", ""),
                    EnableSsl = true,
                };

                // Construir el mensaje de correo electrónico
                MailMessage mailMessage = new MailMessage("example@gmail.com", passengerEmail)
                {
                    Subject = "Confirmación de reserva",
                    Body = "Su reserva ha sido confirmada. Gracias por elegir nuestros servicios."
                };

                // Adjuntar el PDF al correo electrónico
                if (!string.IsNullOrEmpty(pdfFilePath))
                {
                    mailMessage.Attachments.Add(new Attachment(pdfFilePath));
                }

                // Enviar correo electrónico
                smtpClient.Send(mailMessage);

                // Mostrar mensaje de confirmación de envío
                Console.WriteLine("Correos electrónicos de confirmación enviados correctamente.");
            }
            catch (Exception ex)
            {
                // Manejar cualquier error que pueda ocurrir durante el envío del correo electrónico
                Console.WriteLine($"Error al enviar correos electrónicos de confirmación: {ex.Message}");
            }
        }

        static string GenerarPDF(string passengerName, string passengerEmail, string flightDetails)
        {
            try
            {
                // Generar el documento PDF con los detalles de la reserva
                string pdfFilePath = "Confirmacion_Reserva.pdf";

                using (var writer = new PdfWriter(pdfFilePath))
                {
                    using (var pdf = new PdfDocument(writer))
                    {
                        var document = new Document(pdf);

                        // Agregar título
                        document.Add(new Paragraph("Confirmación de Reserva").SetTextAlignment(TextAlignment.CENTER));

                        // Crear una tabla con 2 columnas
                        Table table = new Table(2);
                        table.SetWidth(UnitValue.CreatePercentValue(100));

                        // Agregar las celdas a la tabla
                        table.AddCell(new Cell().Add(new Paragraph("Nombre")));
                        table.AddCell(new Cell().Add(new Paragraph(passengerName)));

                        table.AddCell(new Cell().Add(new Paragraph("Email")));
                        table.AddCell(new Cell().Add(new Paragraph(passengerEmail)));

                        table.AddCell(new Cell().Add(new Paragraph("Detalles del Vuelo")));
                        table.AddCell(new Cell().Add(new Paragraph(flightDetails)));

                        // Agregar la tabla al documento
                        document.Add(table);

                        // Agregar texto sobre la empresa
                        document.Add(new Paragraph("\nGracias por elegir nuestra aerolínea para su viaje. " +
                            "Estamos comprometidos con brindarle el mejor servicio y esperamos darle la bienvenida a bordo pronto."));
                    }
                }

                return pdfFilePath;
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción que pueda ocurrir durante la generación del PDF
                Console.WriteLine($"Error al generar el PDF: {ex.Message}");
                return null; // Retornar null en caso de error
            }
        }

        //static string GenerarPDF(string passengerName, string passengerEmail, string flightDetails)
        //{
        //    try
        //    {
        //        // Generar el documento PDF con los detalles de la reserva
        //        string pdfFilePath = "Confirmacion_Reserva.pdf";

        //        using (var writer = new PdfWriter(pdfFilePath))
        //        {
        //            using (var pdf = new PdfDocument(writer))
        //            {
        //                var document = new Document(pdf);

        //                // Agregar los detalles de la reserva al documento
        //                document.Add(new Paragraph($"Nombre: {passengerName}"));
        //                document.Add(new Paragraph($"Email: {passengerEmail}"));
        //                document.Add(new Paragraph($"Detalles del Vuelo: {flightDetails}"));
        //            }
        //        }

        //        return pdfFilePath;
        //    }
        //    catch (Exception ex)
        //    {
        //        // Manejar cualquier excepción que pueda ocurrir durante la generación del PDF
        //        Console.WriteLine($"Error al generar el PDF: {ex.Message}");
        //        return null; // Retornar null en caso de error
        //    }
        //}
    }
}
