"""Exercise the rental workflow on a freshly migrated local test database with a seeded admin."""

from datetime import datetime, timezone, timedelta
import os
from pathlib import Path
from urllib.parse import urlsplit
from playwright.sync_api import sync_playwright
import secrets

base_url = os.environ["DHAKARENT_TEST_URL"]
if urlsplit(base_url).hostname not in ("localhost", "127.0.0.1", "::1"):
    raise ValueError("Use a fresh local test instance for this data-creating workflow.")
admin_email = os.environ["DHAKARENT_TEST_ADMIN_EMAIL"]
admin_password = os.environ["DHAKARENT_TEST_ADMIN_PASSWORD"]
fixtures = Path(__file__).parent / "fixtures"
today = datetime.now(timezone.utc) + timedelta(hours=6)

with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path=os.getenv("CHROMIUM_PATH"))
    page = browser.new_page(viewport={"width": 1440, "height": 1000}, base_url=base_url)

    def visit(path):
        assert page.goto(path).ok, path
        assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), path

    page.route("https://tile.openstreetmap.org/**", lambda route: route.abort())
    guest = browser.new_context(base_url=base_url).request
    password = "Ui1!" + secrets.token_hex(12)
    for role in ["Tenant", "PropertyOwner"]:
        visit("/Account/Register")
        page.locator("#FullName").fill(f"Test {role}")
        email = role.lower() + secrets.token_hex(3) + "@example.test"
        if role == "Tenant":
            tenant = email
        else:
            owner = email
        page.locator("#Email").fill(email)
        page.locator("#Password").fill(password)
        page.locator("#ConfirmPassword").fill(password)
        page.locator("#RegistrationConsent").check()
        page.locator("#AccountType").select_option(role)
        page.locator("main button[type=submit]").click()
        if role == "Tenant":
            page.get_by_role("link", name="My dashboard").click()
            assert page.get_by_role("heading", name="Tenant dashboard").is_visible()
        else:
            page.get_by_role("link", name="My buildings").click()
            visit("/Building/Create")
            for key, value in [
                ("Name", "Garden Court"),
                ("Area", "Mirpur"),
                ("Address", "42 Garden Road"),
            ]:
                page.locator("#" + key).fill(value)
            page.locator("main button[type=submit]").click()
            assert page.get_by_text("Garden Court", exact=True).is_visible()
            page.locator('a[href*="/Floor?"]').first.click()
            page.locator('a[href*="/Floor/Create"]').first.click()
            assert (
                page.get_by_role("navigation", name="Breadcrumb")
                .get_by_role("link", name="Building floors")
                .is_visible()
            )
            page.locator("#FloorNumber").fill("1")
            page.locator("#Label").fill("First floor")
            page.locator("main button[type=submit]").click()
            page.locator('a[href*="/RentalUnit?"]').first.click()
            page.locator('a[href*="/RentalUnit/Create"]').first.click()
            crumbs = page.get_by_role("navigation", name="Breadcrumb")
            assert "buildingId=" in crumbs.get_by_role(
                "link", name="Building floors"
            ).get_attribute("href")
            assert "floorId=" in crumbs.get_by_role(
                "link", name="Rental units"
            ).get_attribute("href")
            for key, value in [
                ("UnitNumber", "1A"),
                ("SizeInSqFt", "850"),
                ("StandardMonthlyRent", "22000"),
                ("Bedrooms", "2"),
                ("Bathrooms", "1"),
            ]:
                page.locator("#" + key).fill(value)
            page.locator("main button[type=submit]").click()
            assert page.get_by_text("1A", exact=True).is_visible()
            visit("/PropertyOwner/CreateProperty")
            for key, value in [
                ("Title", "Garden apartment"),
                ("Area", "Mirpur"),
                ("Address", "42 Garden Road"),
                ("Description", "Two-bedroom rental in Mirpur."),
                ("SizeInSqFt", "850"),
                ("MonthlyRent", "22000"),
                ("Bedrooms", "2"),
                ("Bathrooms", "1"),
                ("AvailableFrom", (today + timedelta(days=5)).strftime("%Y-%m-%d")),
            ]:
                page.locator("#" + key).fill(value)
            page.locator("#PropertyType").select_option("1")
            page.locator("#UnitId").select_option(index=1)
            page.locator("#Latitude").fill("23.81")
            page.locator("#Longitude").fill("90.41")
            page.locator("#Uploads").set_input_files(
                [
                    fixtures / "sample.png",
                    fixtures / "sample.mp4",
                    fixtures / "plan.pdf",
                ]
            )
            page.locator("main button[type=submit]").click()
            assert page.get_by_text(
                "Garden apartment", exact=True
            ).is_visible(), page.locator("main").inner_text()
        page.get_by_role("button", name="Log out").click()
    assert guest.get("/listing-media/1").status == 404
    assert guest.get("/Properties/Details/1").status == 404
    visit("/Account/Login")
    page.locator("#Email").fill(admin_email)
    page.locator("#Password").fill(admin_password)
    page.locator("main button[type=submit]").click()
    page.get_by_role("link", name="Review queue", exact=True).click()
    assert page.get_by_text("Garden apartment", exact=True).is_visible()
    page.locator('a[href*="/Admin/ReviewProperty"]').first.click()
    page.once("dialog", lambda dialog: dialog.accept())
    page.get_by_role("button", name="Approve Property").click()
    visit("/")
    page.get_by_role("button", name="Search rentals").click()
    page.locator("#filter-area").fill("Mirpur")
    page.locator("#filter-max").fill("25000")
    page.get_by_role("button", name="Search properties").click()
    page.get_by_role("link", name="View details for Garden apartment").click()
    assert page.get_by_role("heading", name="Garden apartment").is_visible()

    assert page.locator("main video").count() == 1
    assert page.locator("[data-map-lat]").count() == 1
    image_url = page.locator("main img.listing-asset").get_attribute("src")
    video_url = page.locator("main video").get_attribute("src")
    assert (
        guest.get("/listing-media/3")
        .headers["content-disposition"]
        .startswith("attachment")
    )
    assert guest.get("/Properties/Details/1").status == 200
    assert guest.get(image_url).body() == (fixtures / "sample.png").read_bytes()
    assert page.request.get(video_url, headers={"Range": "bytes=0-23"}).status == 206

    def login(email):
        page.get_by_role("button", name="Log out").click()
        visit("/Account/Login")
        page.locator("#Email").fill(email)
        page.locator("#Password").fill(password)
        page.locator("main button[type=submit]").click()

    login(owner)
    visit("/PropertyOwner/EditProperty/1")
    page.locator('[name=RemoveMedia][value="3"]').check()
    page.locator("main button[type=submit]").click()
    assert guest.get(image_url).status == 404, (
        page.url,
        page.locator("main").inner_text(),
        page.locator("input:invalid").evaluate_all("els=>els.map(el=>el.name)"),
    )
    page.get_by_role("button", name="Log out").click()
    visit("/Account/Login")
    page.locator("#Email").fill(admin_email)
    page.locator("#Password").fill(admin_password)
    page.locator("main button[type=submit]").click()
    visit("/Admin/ReviewProperty/1")
    page.once("dialog", lambda dialog: dialog.accept())
    page.get_by_role("button", name="Approve Property").click()
    assert guest.get(image_url).status == 200
    assert guest.get("/listing-media/3").status == 404
    guest_page = browser.new_page(base_url=base_url)
    guest_page.goto("/Properties?Area=Mirpur")
    guest_page.get_by_role("button", name="Save to wishlist").click()
    assert guest_page.locator("#account-prompt").is_visible()
    guest_page.close()

    login(tenant)
    visit("/Properties?Area=Mirpur")
    heart = page.get_by_role("button", name="Wishlist for Garden apartment")
    assert heart.get_attribute("aria-pressed") == "false"
    heart.click()
    page.wait_for_selector('.wishlist-toggle[aria-pressed="true"]')
    assert "Area=Mirpur" in page.url
    page.wait_for_function("""getComputedStyle(document.querySelector('[data-wishlist-form] svg')).fill === "rgb(201, 35, 70)" """)
    page.reload()
    assert heart.get_attribute("aria-pressed") == "true"
    visit("/SavedProperty")
    assert "Garden apartment" in page.locator("main").inner_text()
    visit("/Properties?Area=Mirpur")
    heart.click()
    page.wait_for_selector('.wishlist-toggle[aria-pressed="false"]')
    visit("/SavedProperty")
    assert "Garden apartment" not in page.locator("main").inner_text()

    visit("/PropertyInquiry/Create?propertyId=1")
    page.locator("main textarea").fill("Private question about water charges")
    page.locator("main button[type=submit]").click()
    visit("/PropertyInquiry")
    assert owner in page.locator(".contact-person").inner_text()
    visit("/ViewingRequest/Create?propertyId=1")
    page.locator("#RequestedDateTimeLocal").fill((today + timedelta(days=2)).strftime("%Y-%m-%dT14:00"))
    page.locator("#TenantMessage").fill("Private viewing note")
    page.locator("main button[type=submit]").click()
    visit("/ViewingRequest")
    assert owner in page.locator(".contact-person").inner_text()
    login(owner)
    visit("/OwnerInquiry")
    assert tenant in page.locator(".contact-person").inner_text()
    page.locator("[name=replyContent]").fill("Private owner reply")
    page.get_by_role("button", name="Send Reply").click()
    for path in ["/OwnerViewingRequest", "/Messaging", "/Messaging/Details/1"]:
        visit(path)
        assert tenant in page.locator(".contact-person").inner_text()
    login(tenant)
    for path in ["/Messaging", "/Messaging/Details/1"]:
        visit(path)
        assert owner in page.locator(".contact-person").inner_text()
    assert page.request.get("/CommunicationOverview").status == 403
    visit("/Properties/Details/1")
    page.get_by_text("Report this listing").click()
    f = page.locator('form[action$="/Submit"]')
    f.locator("[name=reason]").fill("Incorrect area")
    f.locator("textarea").fill("Please check the address")
    f.locator("button").click()
    login(owner)
    visit("/RentalDesk")
    page.get_by_text("Start a lease", exact=True).click()

    def fill_form(action, values):
        form = page.locator(f'form[action$="/{action}"]')
        for k, v in values.items():
            el = form.locator(f'[name="{k}" i]')
            if el.evaluate("e=>e.tagName") == "SELECT":
                el.select_option(value=v)
            elif el.get_attribute("type") == "checkbox":
                el.set_checked(bool(v))
            else:
                el.fill(v)
        form.locator("button").click()
        assert not page.locator("main [role=alert]").count(), page.locator(
            "main"
        ).inner_text()

    fill_form(
        "Start",
        dict(
            unitId="1",
            tenantEmail=tenant,
            start=today.strftime("%Y-%m-01"),
            rent="18000",
            deposit="18000",
            dueDay="5",
        ),
    )
    assert guest.get("/Properties/Details/1").status == 404
    assert guest.get(image_url).status == 404
    page.get_by_text("Agreement, renewal and deposit").click()
    fill_form(
        "Terms",
        dict(
            agreedUntil=(today + timedelta(days=20)).strftime("%Y-%m-%d"),
            depositObligations="Refund after inspection",
        ),
    )
    visit("/Portfolio")
    page.get_by_text("Add building expense").click()
    fill_form(
        "Expense",
        dict(
            incurredOn=today.strftime("%Y-%m-%d"),
            description="Cleaning supplies",
            amount="800",
        ),
    )
    page.get_by_text("Share an expense equally across leases").click()
    fill_form("Allocate", dict(expenseId="1", month=today.strftime("%Y-%m")))
    page.get_by_text("Manage building access").click()
    fill_form("Assign", dict(email=tenant, accounts=True, repairs=True))
    visit("/RentalDesk")
    page.get_by_text("Issue monthly invoice").click()
    page.get_by_text("Metered electricity").click()
    fill_form(
        "Bill",
        dict(
            water="500",
            gas="1000",
            electricity="0",
            service="0",
            previous="100",
            current="200",
            rate="12",
        ),
    )
    assert "21,500.00" in page.locator("main").inner_text()
    page.get_by_text("Record a received payment").click()
    fill_form("Confirm", dict(amount="10000", reference="Cash receipt 100"))
    assert "11,500.00" in page.locator("main").inner_text()
    assert "10000.00,11500.00" in page.request.get("/RentalDesk/Export").text()
    login(tenant)
    visit("/RentalDesk")
    assert "11,500.00" in page.locator("main").inner_text()
    assert "10,000.00" in page.request.get("/RentalDesk/Receipt/1").text()
    assert "Cleaning supplies" in page.request.get("/BuildingAccounts/Export/1").text()
    fill_form("RequestRepair", dict(description="Kitchen tap is leaking"))
    visit("/AssignedRepairs")
    assert "Kitchen tap is leaking" in page.locator("main").inner_text()
    fill_form("Start", {})
    assert "In progress" in page.locator("main").inner_text()
    visit("/Documents")
    f = page.locator('form[action$="/Upload"]')
    f.locator("[type=file]").set_input_files(fixtures / "plan.pdf")
    f.locator("[name=consent]").check()
    f.locator("button").click()
    assert page.request.get("/Documents/Download/1").body().startswith(b"%PDF-")
    login(owner)
    visit("/RentalDesk")
    fill_form("CompleteRepair", dict(expense="450"))
    assert "450.00" in page.locator("main").inner_text()
    assert page.request.get("/Documents/Download/1").status == 404
    visit("/Portfolio")
    assert "1,250.00" in page.locator("main").inner_text()
    fill_form("Revoke", {})
    login(tenant)
    assert page.request.get("/BuildingAccounts/Export/1").status == 404
    page.get_by_role("button", name="Log out").click()
    visit("/Account/Login")
    page.locator("#Email").fill(admin_email)
    page.locator("#Password").fill(admin_password)
    page.locator("main button[type=submit]").click()
    visit("/Documents")
    page.get_by_role("button", name="Accept for profile").click()
    for kind in ["Conversations", "Inquiries", "Viewings"]:
        visit("/CommunicationOverview?kind=" + kind)
        rendered = page.locator("main").inner_text()
        assert all(value in rendered for value in [tenant, owner, "Test Tenant", "Test PropertyOwner", "Garden apartment"])
        assert all(value not in rendered for value in ["Private owner reply", "Private viewing note", "Private question about water charges"])
    assert page.request.get("/Messaging/Details/1").status == 403
    visit("/ListingReports")
    page.locator("[name=remarks]").fill("Address checked")
    page.get_by_role("button", name="Mark resolved").click()
    visit("/Subscriptions")
    page.get_by_text("Set an owner's plan").click()
    fill_form(
        "Set",
        dict(ownerEmail=owner, plan="Test plan", buildings="1", units="1", fee="100"),
    )
    fill_form("Issue", dict(month=today.strftime("%Y-%m")))
    fill_form("Confirm", dict(reference="Paid at office"))
    login(owner)
    visit("/Subscriptions")
    assert "Confirmed" in page.locator("main").inner_text()
    visit("/Building/Create")
    for key, value in [
        ("Name", "Second building"),
        ("Area", "Mirpur"),
        ("Address", "1 Garden Road"),
    ]:
        page.locator("#" + key).fill(value)
    page.locator("main button[type=submit]").click()
    assert "building limit has been reached" in page.locator("main").inner_text()
    page.get_by_role("button", name="Log out").click()
    visit("/Account/Register")
    page.locator("#FullName").fill("Independent tenant")
    page.locator("#Email").fill(f"other-{secrets.token_hex(4)}@example.test")
    page.locator("#Password").fill(password)
    page.locator("#ConfirmPassword").fill(password)
    page.locator("#RegistrationConsent").check()
    page.locator("main button[type=submit]").click()
    assert page.request.get("/Messaging/Details/1").status == 404
    assert page.request.get("/RentalDesk/Receipt/1").status == 404
    assert page.request.get("/Documents/Download/1").status == 404
    assert page.request.get("/BuildingAccounts/Export/1").status == 404
    visit("/RentalDesk")
    assert "no leases for this account" in page.locator("main").inner_text()
    print(
        "Access, private documents, review, allocation, meter billing, subscription limits and scoped accounting passed."
    )
    print(
        "Owner listing with map, image, MP4, moderation, gallery, range playback, lease, itemized bill, partial payment, receipt, export and maintenance passed."
    )
