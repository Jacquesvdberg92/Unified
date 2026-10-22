# Unified — Support, Roadmap & Terms of Use

## Current Support Status

**Support for this project is currently limited.**

The application has been built to a functional state. However, the author is taking a deliberate break from active development and day-to-day support. Bug reports and feature requests may not receive a timely response unless there is a financial incentive in place.

If you require prioritised support, dedicated bug fixes, custom feature development, or SLA-backed assistance, please reach out directly to discuss a commercial arrangement.

---

## Known Issues

### Dark Mode Anomalies

There are known visual inconsistencies in dark mode. The author is **aware of these issues** and they are on the list to be resolved. They are not forgotten — just deferred while a break is being taken. They will be addressed in a future update.

### Email Template Edit Clears the Body; Copy Signature HTML Does Nothing

Three views write HTML content into a double-quoted `data-` attribute as `@Html.Raw(JsonSerializer.Serialize(...))`. The JSON's own quotes close the attribute early, so the browser always reads it as empty, and the rest of the value spills into stray attributes on the element.

- **Email Templates → Edit** (`Views/EmailTemplates/Edit.cshtml`, `data-initial-html`): the body editor opens empty. Saving replaces the stored body with the empty editor contents. **Workaround:** re-enter the full body before saving an edit.
- **Copy Signature HTML** (`Views/Brands/Manager.cshtml` and `Views/EmailTemplates/BrandManager.cshtml`, `data-sig`): clicking the button throws a JavaScript error and copies nothing. **Workaround:** copy the signature from the preview shown above the button.

Because the spilled text becomes attributes, stored signature or template content can also add attributes, including event handlers, to those elements. Only Brand Managers (signatures) and Brand Managers, Team Leaders and Swiss Army Knife users (templates) can edit that content, and it is already rendered unsanitised elsewhere, so this will be fixed together with sanitising stored HTML. The fix is to drop `Html.Raw` so Razor encodes the JSON for the attribute; the page scripts read it back unchanged.

### CS Messaging: Removed Members Keep Receiving Live Updates

When a member is removed from a CS Messaging conversation, any browser tab they already have open stays subscribed to that conversation's real-time updates. Until that connection drops or they reload the page, they continue to see new messages, edits, reactions and member changes for the conversation. Opening the conversation or loading its history is blocked as soon as they are removed; only the already-open live connection is affected. A fix requires tracking each user's open connections so they can be removed from the conversation group (`conv-{id}` in `CsMessagingHub`) at the time of removal.

---

## Finance Department Roadmap

The author remains **committed to improving the Finance department workflow** within Unified. However, meaningful improvements require a thorough understanding of the actual day-to-day workflow used by the Finance team.

Before features can be scoped and built, the following needs to happen:

1. A structured walkthrough or documentation of the current Finance workflow
2. Identification of the biggest pain points and bottlenecks
3. Agreement on what "improved" looks like in practice

If you are part of the Finance team or a stakeholder, please document or communicate your current workflow so that development can be prioritised effectively when the next development cycle begins.

---

## Public Use & Licensing

This project is made **publicly available** for use. You are welcome to run it, deploy it, and adapt it for your own purposes, subject to the following non-negotiable conditions:

### You MAY:
- Run the application for personal or internal business use
- Fork and modify the code for your own use
- Contribute improvements back to the original repository

### You MAY NOT:
- **Remove, obscure, or alter attribution** to the original author (Jacques van den Berg / [@Jacquesvdberg92](https://github.com/Jacquesvdberg92))
- **Sell, sublicense, or commercially distribute** this software or any derivative of it without explicit written permission from the author
- Misrepresent the origin of the software

### Enforcement

Violations of the above will be pursued through legal channels. The author has a documented history of successfully pursuing legal action against significantly larger organisations — including financial institutions. This is not a deterrent notice; it is a statement of fact.

**If you are unsure whether your intended use is permitted, ask first.**

---

## Contact

For commercial support enquiries, licensing questions, or Finance workflow discussions, reach out via GitHub:

[https://github.com/Jacquesvdberg92](https://github.com/Jacquesvdberg92)
