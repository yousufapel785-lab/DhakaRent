for (const element of document.querySelectorAll(".listing-map")) {
    const results = element.closest("[data-results-map]");
    if (results) {
        results.addEventListener("toggle", function initialize() {
            if (!results.open) return;
            results.removeEventListener("toggle", initialize);
            const map = L.map(element, {scrollWheelZoom: false});
            L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            }).addTo(map);
            const bounds = L.latLngBounds();
            for (const link of results.querySelectorAll("[data-lat]")) {
                const point = [Number(link.dataset.lat), Number(link.dataset.lon)];
                bounds.extend(point);
                L.circleMarker(point, {color: "#165c49", radius: 8}).addTo(map).bindPopup(link.cloneNode(true));
            }
            map.fitBounds(bounds.pad(0.1), {maxZoom: 16});
        });
        continue;
    }
    const picker = element.hasAttribute("data-map-picker");
    const lat = document.querySelector("#Latitude");
    const lon = document.querySelector("#Longitude");
    const coordinates = () => picker ? [lat.valueAsNumber, lon.valueAsNumber] :
        [Number(element.dataset.mapLat), Number(element.dataset.mapLon)];
    const point = coordinates();
    const hasPin = point.every(Number.isFinite);
    const map = L.map(element, { scrollWheelZoom: false }).setView(hasPin ? point : [23.8103, 90.4125], hasPin ? 16 : 12);
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(map);
    let pin;
    function place(position) {
        if (pin) pin.setLatLng(position);
        else pin = L.circleMarker(position, { radius: 9, color: "#fff", fillColor: "#165c49", fillOpacity: 1 }).addTo(map);
    }
    if (hasPin) place(point);
    if (picker) {
        map.on("click", event => {
            lat.value = event.latlng.lat.toFixed(6);
            lon.value = event.latlng.wrap().lng.toFixed(6);
            place(coordinates());
        });
        function update() {
            const position = coordinates();
            if (position.every(Number.isFinite) && lat.validity.valid && lon.validity.valid) {
                place(position);
                map.panTo(position);
            } else if (pin) { pin.remove(); pin = null; }
        }
        const locate = element.parentElement.querySelector("[data-use-location]");
        const status = element.parentElement.querySelector("[data-location-status]");
        const lookup = element.parentElement.querySelector("[data-geocode]");
        if (lookup) {
            lookup.addEventListener("click", async () => {
                const form = lookup.form;
                const address = [form.elements.Address.value.trim(), form.elements.Area.value.trim()].filter(Boolean).join(", ");
                if (!address) { status.textContent = "Enter the apartment address first."; return; }
                lookup.disabled = true;
                status.textContent = "Looking for the address…";
                try {
                    const body = new URLSearchParams({address, __RequestVerificationToken: form.querySelector('[name="__RequestVerificationToken"]').value});
                    const response = await fetch(lookup.dataset.geocode, {method: "POST", body});
                    if (!response.ok) throw new Error("Address unavailable");
                    const result = await response.json();
                    lat.value = Number(result.latitude).toFixed(6);
                    lon.value = Number(result.longitude).toFixed(6);
                    update();
                    status.textContent = "Check the location and click the map to correct the pin.";
                } catch {
                    status.textContent = "The address could not be located. Place the pin or enter coordinates.";
                } finally { lookup.disabled = false; }
            });
        }
        if (locate && navigator.geolocation) {
            locate.addEventListener("click", () => {
                status.textContent = "Getting your location…";
                navigator.geolocation.getCurrentPosition(position => {
                    lat.value = position.coords.latitude.toFixed(6);
                    lon.value = position.coords.longitude.toFixed(6);
                    update();
                    status.textContent = "Check the pin before submitting.";
                }, () => { status.textContent = "Choose the location on the map or enter both coordinates."; },
                { timeout: 10000 });
            });
        } else if (locate) { locate.hidden = true; }
        lat.addEventListener("change", update);
        lon.addEventListener("change", update);
    }
}
