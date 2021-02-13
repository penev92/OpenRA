function populateAssets(selectedPackage) {
    let select = document.getElementById("selectAsset");
    select.innerHTML = null;

    let assets = JSON.parse(localStorage.getItem("assetsByPackage"))[selectedPackage];
    if (assets !== null)
    {
        assets.forEach(assetName => {
            let element = document.createElement("option");
            element.textContent = assetName;
            element.value = assetName;
            select.appendChild(element);
        });
    }

    updatePaletteServerState("unittem.pal");

    if (select.options.length > 0) {
        currentSpriteName = select.options[0].value;
        requestSpriteFramesCount(currentSpriteName)
        requestAsset(currentSpriteName);
    }
};

function setPalette(selectedPalette) {
    updatePaletteServerState(selectedPalette);
};

function nextFrame() {
    currentSpriteFrame++;
    if (currentSpriteFrame >= currentSpriteFramesCount) {
        currentSpriteFrame = 0;
    }

    requestAsset(currentSpriteName);
};

function updateCurrentAsset(selectedAsset) {
    currentSpriteFrame = 0;
    currentSpriteName = selectedAsset;
    requestSpriteFramesCount(currentSpriteName);
    requestAsset(currentSpriteName);
};
