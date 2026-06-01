using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

namespace QuoteTrack.Application.Integration
{
    // Interface for Dependency Injection
    public interface IGraphCalendarService
    {
        Task<List<Event>> GetUpcomingMeetingsAsync(int daysAhead = 7);
        Task<bool> ScheduleMeetingAsync(string subject, string content, DateTime start, DateTime end, List<string> attendeeEmails);
    }

    public class GraphCalendarService : IGraphCalendarService
    {
        private readonly GraphServiceClient _graphClient;

        public GraphCalendarService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
        }

        public async Task<List<Event>> GetUpcomingMeetingsAsync(int daysAhead = 7)
        {
            try
            {
                // Note: This requires a valid user token to be present in the context
                var events = await _graphClient.Me.Events
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "subject", "body", "start", "end", "location" };
                        requestConfiguration.QueryParameters.Orderby = new string[] { "start/dateTime" };
                        requestConfiguration.QueryParameters.Filter = $"start/dateTime ge '{DateTime.UtcNow.ToString("s")}'";
                        requestConfiguration.QueryParameters.Top = 50;
                    });

                return events?.Value ?? new List<Event>();
            }
            catch (Exception ex)
            {
                // Log failure (e.g., user hasn't linked their M365 account yet)
                Console.WriteLine($"Graph API Error: {ex.Message}");
                return new List<Event>();
            }
        }

        public async Task<bool> ScheduleMeetingAsync(string subject, string content, DateTime start, DateTime end, List<string> attendeeEmails)
        {
            try
            {
                var attendees = new List<Attendee>();
                foreach (var email in attendeeEmails)
                {
                    attendees.Add(new Attendee
                    {
                        EmailAddress = new EmailAddress { Address = email },
                        Type = AttendeeType.Required
                    });
                }

                var newEvent = new Event
                {
                    Subject = subject,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = content
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = start.ToString("o"),
                        TimeZone = "Arab Standard Time" // Automatically aligning to your local time
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = end.ToString("o"),
                        TimeZone = "Arab Standard Time"
                    },
                    Location = new Location
                    {
                        DisplayName = "Microsoft Teams Meeting"
                    },
                    Attendees = attendees,
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness
                };

                await _graphClient.Me.Events.PostAsync(newEvent);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to schedule meeting: {ex.Message}");
                return false;
            }
        }
    }
}