function populatePalettes() {
    let select = document.getElementById("selectPalette");
    select.innerHTML = null;

    let palettes = JSON.parse(localStorage.getItem("palettes"));
    if (palettes !== null) {
        palettes.forEach(paletteName => {
            let element = document.createElement("option");
            element.textContent = paletteName;
            element.value = paletteName;
            select.appendChild(element);
        });
    }
};

function readStream(readableStream) {
    let reader = readableStream.getReader();

    // read() returns a promise that resolves when a value has been received.
    reader.read().then(
        function onAudioChunkRead({ done, value }) {
            if (done) {
                reader = null;
                return;
            }

            playAudioChunk(value);

            // Read some more, and call this function again
            return reader.read().then(onAudioChunkRead);
        }
    );
};

function readVideoStream(readableStream) {
    let reader = readableStream.getReader();

    // read() returns a promise that resolves when a value has been received.
    reader.read().then(
        function onVideoChunkRead({ done, value }) {
            if (done) {
                reader = null;
                return;
            }

            playVideoChunk(value);

            // Read some more, and call this function again
            return reader.read().then(onVideoChunkRead);
        }
    );
};

function playAudioChunk(byteArray, sampleRate, latency) {
    let floatArray = byteArrayToFloat32Array(byteArray);

    let audioBuffer = AUDIO_CONTEXT.createBuffer(2, floatArray.length / 2, 22050);

    audioBuffer.copyToChannel(floatArray.filter((element, index) => { return index % 2 === 0; }), 0);
    audioBuffer.copyToChannel(floatArray.filter((element, index) => { return index % 2 !== 0; }), 1);

    floatArray = null;

    let source = AUDIO_CONTEXT.createBufferSource();
    source.buffer = audioBuffer;

    source.connect(AUDIO_CONTEXT.destination);

    if (audioChunkStartTime === 0)
        audioChunkStartTime = AUDIO_CONTEXT.currentTime;

    source.start(audioChunkStartTime);
    audioChunkStartTime += audioBuffer.duration;

    audioBuffer = null;
};

function drawVideoChunk(byteArray) {

};

// Hacky way to initialize a Float32Array from a Uint8Array by tieing the float array
// to a new byte array via an ArrayBuffer and filling the new byte array with data.
function byteArrayToFloat32Array(rawByteArray) {
    let arrayBuffer = new ArrayBuffer(rawByteArray.length);
    let floatArray = new Float32Array(arrayBuffer);
    let byteArray = new Uint8Array(arrayBuffer);

    rawByteArray.forEach(function (b, i) {
        byteArray[i] = b;
    });

    arrayBuffer = null;
    byteArray = null;

    return floatArray;
};
