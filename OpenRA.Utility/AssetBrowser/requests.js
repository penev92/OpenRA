function requestAsset(selectedAsset) {
   // document.getElementById("spriteAssetPreview").style.display = "none"
    document.getElementById("spriteAssetPreview").src = null;

    let message = {
        CommandName: "LoadAsset",
        AssetName: selectedAsset,
        FrameNumber: currentSpriteFrame.toString()
    };

    ws.send(JSON.stringify(message));
};

function requestSpriteFramesCount(selectedAsset) {
    let message = {
        CommandName: "GetSpriteFramesCount",
        AssetName: selectedAsset
    };

    ws.send(JSON.stringify(message));
};

function updatePaletteServerState(selectedPalette) {
    let paletteName = "temperat.pal";
    // let paletteName = "PALETTE.BIN";
    // let paletteName = "unittem.pal";
    let message = {
        CommandName: "UpdateState",
        PaletteName: paletteName
    };

    ws.send(JSON.stringify(message));
};
