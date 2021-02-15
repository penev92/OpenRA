function handleIncomingAssetPackages(packages) {
    let assetsByPackage = { "all": [] };
    let palettes = [];
    let select = document.getElementById("selectPackage");

    let vid = document.getElementById("myVideoPlayer");
    console.log(`mp4 - ${vid.canPlayType("audio/mp4")}`);
    console.log(`avi - ${vid.canPlayType("audio/avi")}`);
    console.log(`mkv - ${vid.canPlayType("audio/mkv")}`);
    console.log(`mov - ${vid.canPlayType("audio/mov")}`);
    console.log(`wmv - ${vid.canPlayType("audio/wmv")}`);
    console.log(`mpeg - ${vid.canPlayType("audio/mpeg")}`);

    //vid.src = "https://openra.kempf.dev/videos/red-alert-sizzle.webm";
    //vid.src = "file:///D:/Work.Personal/OpenRA/red-alert-sizzle.webm";
    vid.src = "file:///D:/Work.Personal/OpenRA/drop.avi";
    vid.play();

    for (let packageName in packages) {
        let element = document.createElement("option");
        element.textContent = packageName;
        element.value = packageName;
        select.appendChild(element);
        assetsByPackage[packageName] = [];
        for (let index in packages[packageName]) {
            let assetName = packages[packageName][index];
            assetsByPackage[packageName].push(assetName);
            assetsByPackage["all"].push(assetName);
            if (assetName.endsWith(".pal")) {
                palettes.push(assetName);
            }
        };
    };

    window.localStorage.setItem("assetsByPackage", JSON.stringify(assetsByPackage));
    window.localStorage.setItem("palettes", JSON.stringify(palettes));
    populatePalettes();
};

function handleIncomingSpriteAssetData(dataBlob) {
    document.getElementById("spriteAssetPreview").src = URL.createObjectURL(dataBlob, "image/png");
    //document.getElementById("spriteAssetPreview").style.display = "";

    document.getElementById("textField").value = `Frame ${currentSpriteFrame} / ${currentSpriteFramesCount}`;
};

function handleIncomingAudioAssetData(dataBlob) {
    let audioStream = dataBlob.stream();
    readStream(audioStream);
    dataBlob = null;
};

function handleIncomingVideoAssetData(dataBlob) {
    // document.getElementById("spriteAssetPreview").src = URL.createObjectURL(dataBlob, "image/png");
    // dataBlob = null;
};

function handleIncomingMessageDefault(message) {
    let inc = document.getElementById("incomming");
    inc.innerHTML += message + '<br/>';
};
