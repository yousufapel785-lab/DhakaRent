const fileInput = document.querySelector("#Uploads");
const preview = document.querySelector("[data-upload-preview]");
if (fileInput && preview) {
    let urls = [];
    fileInput.addEventListener("change", () => {
        for (const url of urls) URL.revokeObjectURL(url);
        urls = [];
        preview.replaceChildren();
        for (const file of fileInput.files) {
            const item = document.createElement("figure");
            item.className = "col-6 col-md-4 mb-1";
            if (file.type.startsWith("image/") || file.type.startsWith("video/")) {
                const image = file.type.startsWith("image/");
                const media = document.createElement(image ? "img" : "video");
                media.className = "listing-preview";
                if (image) media.alt = "";
                else { media.controls = true; media.preload = "metadata"; }
                const url = URL.createObjectURL(file);
                urls.push(url);
                media.src = url;
                item.append(media);
            }
            const caption = document.createElement("figcaption");
            caption.className = "small text-break";
            caption.textContent = file.name;
            item.append(caption);
            preview.append(item);
        }
    });
}
