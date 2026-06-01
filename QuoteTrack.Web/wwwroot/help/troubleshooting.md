---
title: Troubleshooting
category: Troubleshooting
summary: Common issues and what to check first.
---

# IMAP ingestion not working
Check:
- Mailbox settings in System Settings
- IMAP port 993 allowed
- System Logs for ingestion startup messages

# Database column errors
This usually means the site was published but DB migrations were not applied correctly.
Fix:
- Apply migrations using `Update-Database` with correct context and connection string.

# Wrong quote value extracted
PDF format differences can cause extraction issues.
Fix:
- Verify value manually in Quote Details
- V3 will improve parsing rules for Zoho + manual quote formats