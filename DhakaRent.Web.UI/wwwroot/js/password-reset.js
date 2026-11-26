if (location.hash) {
    const params = new URLSearchParams(location.hash.slice(1));
    document.querySelector("#Email").value = params.get("email") || "";
    document.querySelector("#Token").value = params.get("token") || "";
    history.replaceState(null, "", location.pathname);
}
