# Rental operations and the seven-part package

The archive supplied a proposed design and a sample building CRUD implementation. The active MVC project already had Identity, buildings, floors, units, listing moderation, search, wishlists, inquiries, private conversations and viewings. The changes extend those entry points. Existing listings can keep an empty unit link until their owner associates a unit.

## Listing an apartment

1. From **My buildings**, create the building, floor and vacant unit.
2. From **My listings**, create a property and select its unit. Click the map or enter latitude and longitude together. **Use my current location** asks the browser for a position the owner can correct.
3. Attach up to eight public files. Photos support JPEG, PNG and WebP. Videos support MP4 and WebM. A PDF can supply a downloadable floor plan. The limit is 24 MB per file and 30 MB per submission. Existing files can be removed during an edit.
4. Submit for admin review. Approval publishes the address, pin and media. Editing a published listing returns it to review. Starting a lease, marking a unit under maintenance or archiving its building hides its linked listings from public browsing.

The map uses Leaflet and OpenStreetMap tiles. Numeric coordinates remain available when tiles cannot load. Listing files are served from SQL Server through visibility-checked requests. Lease agreements and tenant profile documents have a separate private upload and access policy.

## Reference requirements

| Package requirements | Available workflow | Deployment or scope detail |
| --- | --- | --- |
| FR-01, FR-05, FR-23 | Role-based registration, account-use consent, email password reset, encrypted private documents, internal review, withdrawal, expiry and audited private downloads | Configure mail for delivery and publish a retention policy before enabling private uploads. Existing accounts have no retroactive consent timestamp. |
| FR-02–04, FR-12, FR-22 | Building and unit inventory, moderation, search, maps, wishlist, inquiries, private chat and viewings | Owners can confirm or correct pins. Optional address lookup uses a configured Nominatim-compatible search endpoint. |
| FR-06–09, FR-14 | Tenant-specific current and historical leases, renewal date, agreed deposit obligations, move-out, protected agreements, itemized invoices, manual payments, receipts and outstanding balances | A payment applies to one invoice. Deposit obligations are recorded as text. |
| FR-10–11, FR-18, FR-20 | Building occupancy, active rent, monthly collections, arrears, operating expenses, repair progress and rent, tenant and expense CSV reports | Owners explicitly grant or revoke repair and accounting permissions per building. |
| FR-13 | Listing reports, reasoned moderation decisions, report resolution and privileged-action audit records | Report resolution and listing approval have separate actions. |
| FR-15, FR-17 | Optional recurring base-rent invoices, in-app due, overdue, payment and lease-expiry notices, manual utility lines, metered electricity and equal allocation of building expenses | Enter both meter readings and the applicable tariff for the billing month. Allocate before issuing invoices. Any fractional cents are distributed in lease-ID order. Meter integrations and SMS delivery require separate services. |
| FR-19 | Admin-assigned plan terms, optional trials, inventory limits, subscription invoices and confirmed manual collections | Prices and limits are entered by the admin. Assignments apply to new inventory. An owner with no assigned plan retains existing access. |
| FR-16 | Manual rent and subscription collections | Live gateway checkout, webhook settlement and refunds need the chosen provider's API, merchant agreement, credentials and settlement policy. |
| FR-21 | The repository contains prediction history storage | Tenant inference needs the thesis model or endpoint, preprocessing contract, feature schema and model version. |
| FR-24–25 | Internal document review | Official NID, tax and ownership checks require authorized providers and permission to process those records. |

## Deployment

The Visual Studio Development launch profiles apply pending SQL Server migrations to `DefaultConnection` before seeding roles and looking up the admin user. The development database account needs permission to apply those migrations.

For staging and production, use .NET 10 and the matching EF Core tools. Configure the target `ConnectionStrings__DefaultConnection` through the environment. Generate and review the incremental SQL against staging, then apply the migration before starting the updated web app.

```sh
dotnet ef migrations script --idempotent --project DhakaRent.DataAccess --startup-project DhakaRent.Web.UI --output rental-operations.sql
dotnet ef database update --project DhakaRent.DataAccess --startup-project DhakaRent.Web.UI
```

- Set `PrivateDocuments__PolicyVersion` and `PrivateDocuments__RetentionDays` (1–3650) to the site's approved values. Configure a durable, protected ASP.NET Core Data Protection key ring shared by application instances before retaining documents. Losing those keys prevents decryption.
- To enable address lookup, set `Geocoding__SearchEndpoint` to the HTTPS search URL of a self-hosted or contracted Nominatim-compatible service. The form identifies the host and sends an address only when the owner requests a lookup.
- Password recovery uses `Mail__PublicOrigin` (canonical HTTPS origin), `Mail__Host`, `Mail__Port`, `Mail__From` and optional `Mail__Username` and `Mail__Password`. The SMTP transport requires TLS.
- The web host runs scheduled work on startup and every 15 minutes while running. The job catches up on overdue recurring invoice periods, records in-app reminders and deletes expired document rows and content. Expired documents immediately fail download authorization. Backup retention is managed separately.
- To run the role-based browser regression suite against a fresh local database, follow [the test instructions](../tests/README.md).
