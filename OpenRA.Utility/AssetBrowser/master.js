const port = 6464;

let currentAssetType = undefined;
let currentSpriteName = undefined;
let currentSpriteFrame = 0;
let currentSpriteFramesCount = 0;

function initialize() {
    let inc = document.getElementById('incomming');
    let wsImpl = window.WebSocket || window.MozWebSocket;

    inc.innerHTML += "connecting to server ..<br/>";

    // create a new websocket and connect
    window.ws = new wsImpl(`ws://localhost:${port}/`);

    // when data is comming from the server, this metod is called
    ws.onmessage = function (evt) {
        if (typeof(evt.data) === "string") {
            let message = JSON.parse(evt.data);
            let command = message["CommandName"];
            let payload = message["Data"];

            if (command === "ListPackages") {
                handleIncomingAssetPackages(payload);
            }
            else if (command === "SendingAsset") {
                currentAssetType = payload["AssetType"];
            }
            else if (command === "GetSpriteFramesCount") {
                currentSpriteFramesCount = payload;
            }
            else {
                handleIncomingMessageDefault(payload);
            }
        }
        else {
            switch (currentAssetType) {
                case "Sprite":
                    handleIncomingSpriteAssetData(evt.data);
                    break;

                case "Audio":
                    handleIncomingAudioAssetData(evt.data);
                    break;

                default:
                    break;
            }
        }
    };

    // when the connection is established, this method is called
    ws.onopen = function () {
        inc.innerHTML += '.. connection open<br/>';
    };

    // when the connection is closed, this method is called
    ws.onclose = function () {
        inc.innerHTML += '.. connection closed<br/>';
    }
};

// This will go away once we get more modern.
function include(file) {
    let script  = document.createElement('script');
    script.src  = file;
    script.type = 'text/javascript';
    script.defer = true;

    document.getElementsByTagName('head').item(0).appendChild(script);
};

include("messageHandlers.js");
include("misc.js");
include("requests.js");
include("userInputHandlers.js");

window.onload = initialize;
