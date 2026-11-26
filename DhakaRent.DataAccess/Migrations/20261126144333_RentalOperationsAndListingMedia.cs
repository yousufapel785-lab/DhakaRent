using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhakaRent.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RentalOperationsAndListingMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Properties",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Properties",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitId",
                table: "Properties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationConsentUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationConsentVersion",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingStaff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    StaffUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CanReadAccounts = table.Column<bool>(type: "bit", nullable: false),
                    CanManageRepairs = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingStaff_AspNetUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuildingStaff_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AgreedUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    TermRevision = table.Column<int>(type: "int", nullable: false),
                    NextRentInvoiceMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    DepositObligations = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DueDay = table.Column<int>(type: "int", nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deposit = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leases_AspNetUsers_TenantId",
                        column: x => x.TenantId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leases_RentalUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "RentalUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingMedia_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperatingExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    IncurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatingExpenses_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OwnerSubscription",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BuildingLimit = table.Column<int>(type: "int", nullable: false),
                    UnitLimit = table.Column<int>(type: "int", nullable: false),
                    TrialThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    MonthlyFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerSubscription", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_OwnerSubscription_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentalNotice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalNotice", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaseAgreementId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Leases_LeaseAgreementId",
                        column: x => x.LeaseAgreementId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTicket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaseAgreementId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Expense = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTicket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceTicket_Leases_LeaseAgreementId",
                        column: x => x.LeaseAgreementId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrivateDocument",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LeaseAgreementId = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Review = table.Column<int>(type: "int", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivateDocument_AspNetUsers_TenantId",
                        column: x => x.TenantId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrivateDocument_Leases_LeaseAgreementId",
                        column: x => x.LeaseAgreementId,
                        principalTable: "Leases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ListingMediaContent",
                columns: table => new
                {
                    ListingMediaId = table.Column<int>(type: "int", nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingMediaContent", x => x.ListingMediaId);
                    table.ForeignKey(
                        name: "FK_ListingMediaContent_ListingMedia_ListingMediaId",
                        column: x => x.ListingMediaId,
                        principalTable: "ListingMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedCharge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatingExpenseId = table.Column<int>(type: "int", nullable: false),
                    LeaseAgreementId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedCharge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedCharge_Leases_LeaseAgreementId",
                        column: x => x.LeaseAgreementId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedCharge_OperatingExpenses_OperatingExpenseId",
                        column: x => x.OperatingExpenseId,
                        principalTable: "OperatingExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionMonth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ConfirmedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionMonth", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionMonth_OwnerSubscription_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "OwnerSubscription",
                        principalColumn: "OwnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceCharge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RentInvoiceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceCharge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceCharge_Invoices_RentInvoiceId",
                        column: x => x.RentInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RentInvoiceId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_RentInvoiceId",
                        column: x => x.RentInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrivateDocumentContent",
                columns: table => new
                {
                    PrivateDocumentId = table.Column<int>(type: "int", nullable: false),
                    Ciphertext = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateDocumentContent", x => x.PrivateDocumentId);
                    table.ForeignKey(
                        name: "FK_PrivateDocumentContent_PrivateDocument_PrivateDocumentId",
                        column: x => x.PrivateDocumentId,
                        principalTable: "PrivateDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_UnitId",
                table: "Properties",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingStaff_BuildingId_StaffUserId",
                table: "BuildingStaff",
                columns: new[] { "BuildingId", "StaffUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingStaff_StaffUserId",
                table: "BuildingStaff",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceCharge_RentInvoiceId",
                table: "InvoiceCharge",
                column: "RentInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_LeaseAgreementId_Month",
                table: "Invoices",
                columns: new[] { "LeaseAgreementId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leases_TenantId",
                table: "Leases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_UnitId",
                table: "Leases",
                column: "UnitId",
                unique: true,
                filter: "[EndDate] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ListingMedia_PropertyId",
                table: "ListingMedia",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTicket_LeaseAgreementId",
                table: "MaintenanceTicket",
                column: "LeaseAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingExpenses_BuildingId",
                table: "OperatingExpenses",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RentInvoiceId_Reference",
                table: "Payments",
                columns: new[] { "RentInvoiceId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivateDocument_ExpiresUtc",
                table: "PrivateDocument",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateDocument_LeaseAgreementId",
                table: "PrivateDocument",
                column: "LeaseAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateDocument_TenantId",
                table: "PrivateDocument",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalNotice_EventKey",
                table: "RentalNotice",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalNotice_UserId_CreatedUtc",
                table: "RentalNotice",
                columns: new[] { "UserId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedCharge_LeaseAgreementId",
                table: "SharedCharge",
                column: "LeaseAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedCharge_OperatingExpenseId_LeaseAgreementId",
                table: "SharedCharge",
                columns: new[] { "OperatingExpenseId", "LeaseAgreementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionMonth_OwnerId_Month",
                table: "SubscriptionMonth",
                columns: new[] { "OwnerId", "Month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_RentalUnits_UnitId",
                table: "Properties",
                column: "UnitId",
                principalTable: "RentalUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_RentalUnits_UnitId",
                table: "Properties");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "BuildingStaff");

            migrationBuilder.DropTable(
                name: "InvoiceCharge");

            migrationBuilder.DropTable(
                name: "ListingMediaContent");

            migrationBuilder.DropTable(
                name: "MaintenanceTicket");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PrivateDocumentContent");

            migrationBuilder.DropTable(
                name: "RentalNotice");

            migrationBuilder.DropTable(
                name: "SharedCharge");

            migrationBuilder.DropTable(
                name: "SubscriptionMonth");

            migrationBuilder.DropTable(
                name: "ListingMedia");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "PrivateDocument");

            migrationBuilder.DropTable(
                name: "OperatingExpenses");

            migrationBuilder.DropTable(
                name: "OwnerSubscription");

            migrationBuilder.DropTable(
                name: "Leases");

            migrationBuilder.DropIndex(
                name: "IX_Properties_UnitId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "RegistrationConsentUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegistrationConsentVersion",
                table: "AspNetUsers");
        }
    }
}
