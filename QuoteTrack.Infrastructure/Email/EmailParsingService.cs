using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Infrastructure.Email
{
    public class EmailParsingService
    {
        private readonly string[] _internalDomains;

        public EmailParsingService(IConfiguration config)
        {
            // Locked in the 3 official Nexcel domains to filter out internal reps
            _internalDomains = new[] { "@nexcel.me", "@nexcelservice.net", "@nexcelbahrain.com" };
        }

        // --- PHASE 1: LEAD INGESTION (SMART SCANNING) ---
        public Rfq ExtractClientDetailsFromForward(string senderEmail, string subject, string plainTextBody)
        {
            var rfq = new Rfq
            {
                OriginalForwarderEmail = senderEmail,
                Subject = subject.Replace("Fwd:", "").Replace("FW:", "").Replace("RE:", "").Trim(),
                Body = plainTextBody // Default fallback
            };

            // 1. Gather all potential emails: The literal sender + anything found inside the email body text
            var allFoundEmails = new List<string> { senderEmail };
            var emailRegex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.IgnoreCase);

            foreach (Match match in emailRegex.Matches(plainTextBody))
            {
                allFoundEmails.Add(match.Value);
            }

            // 2. Scan and isolate the first EXTERNAL email (The true Client)
            string? targetClientEmail = null;
            foreach (var email in allFoundEmails)
            {
                string cleanEmail = email.ToLower().Trim();
                bool isInternal = _internalDomains.Any(domain => cleanEmail.EndsWith(domain));

                if (!isInternal)
                {
                    targetClientEmail = cleanEmail;
                    break; // Found the external client!
                }
            }

            rfq.ClientEmail = targetClientEmail ?? "Pending Manual Entry";

            // 3. Try to extract the Client Name next to the found email
            if (targetClientEmail != null)
            {
                string escapedEmail = Regex.Escape(targetClientEmail);
                // Looks for formats like: "From: John Doe <john@external.com>" or "Name: John Doe Email: john@external.com"
                var nameMatch = Regex.Match(plainTextBody, $@"(?:From|Name):\s*(?<name>[A-Za-z\s]+)?\s*[<\[]?{escapedEmail}[>\]]?", RegexOptions.IgnoreCase);

                if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups["name"].Value))
                {
                    rfq.ClientName = nameMatch.Groups["name"].Value.Replace("\"", "").Trim();
                }
                else
                {
                    // Fallback: Capitalize the first part of their email (e.g., john.doe@... -> John Doe)
                    string prefix = targetClientEmail.Split('@')[0].Replace(".", " ");
                    rfq.ClientName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(prefix);
                }
            }

            // 4. Extract Query/Requirements (Clean up the body by removing forwarding headers)
            // If it's a forwarded thread, the actual client query is usually after the last "Subject:" line
            int lastSubjectIndex = plainTextBody.LastIndexOf("Subject:", StringComparison.OrdinalIgnoreCase);
            if (lastSubjectIndex > -1)
            {
                // Find the end of the "Subject: ..." line and grab the actual query text below it
                int endOfLine = plainTextBody.IndexOf('\n', lastSubjectIndex);
                if (endOfLine > -1 && endOfLine < plainTextBody.Length)
                {
                    string extractedQuery = plainTextBody.Substring(endOfLine).Trim();

                    if (!string.IsNullOrWhiteSpace(extractedQuery))
                    {
                        rfq.Body = extractedQuery;
                    }
                }
            }

            // 5. Hunt for Phone Numbers
            var phoneMatch = Regex.Match(plainTextBody, @"(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}");
            if (phoneMatch.Success) rfq.ContactNumber = phoneMatch.Value.Trim();

            // 6. Hunt for Websites
            var webMatch = Regex.Match(plainTextBody, @"(https?:\/\/)?(www\.)?([a-zA-Z0-9]+(-?[a-zA-Z0-9])*\.)+[\w]{2,}(\/\S*)?", RegexOptions.IgnoreCase);
            if (webMatch.Success)
            {
                string website = webMatch.Value.ToLower();
                // Ensure we don't accidentally grab Nexcel's own website from an employee signature
                bool isInternalWeb = _internalDomains.Any(domain => website.Contains(domain.Replace("@", "")));
                if (!isInternalWeb) rfq.Website = website;
            }

            return rfq;
        }

        // --- PHASE 2: QUOTE INGESTION ---
        public Quote ExtractQuoteDetails(string senderEmail, string recipientEmail, string subject, string plainTextBody, string messageId)
        {
            var quote = new Quote
            {
                SenderEmail = senderEmail,
                ClientName = recipientEmail,
                Subject = subject,
                EmailSentDateTime = DateTime.UtcNow,
                EmailMessageId = messageId,
                Status = QuoteStatus.New
            };

            var refMatch = Regex.Match(subject, @"(EST|QT|REF)[-\s]?\d{2}[-\s]?\d{4,5}", RegexOptions.IgnoreCase);
            if (refMatch.Success) quote.QuoteReference = refMatch.Value.ToUpper();

            var valueMatch = Regex.Match(plainTextBody, @"(?:BHD|Total|Amount|Value)[:\s]+([\d,]+\.\d{2,3})", RegexOptions.IgnoreCase);
            if (valueMatch.Success && decimal.TryParse(valueMatch.Groups[1].Value.Replace(",", ""), out decimal parsedValue))
            {
                quote.QuoteValue = parsedValue;
                quote.Currency = "BHD";
            }

            return quote;
        }
    }
}