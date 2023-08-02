const locationChart = new Chart(document.getElementById("location-chart"), {
    type: 'line',
    options: {
        responsive: true,
    },
    data: {
        labels: [],
        datasets: [{
            label: "Error",
            data: [],
            fill: false,
            borderColor: 'rgb(75, 192, 192)'
        }]
    }
});

const speedChart = new Chart(document.getElementById("speed-chart"), {
    type: 'line',
    options: {
        responsive: true,
    },
    data: {
        labels: [],
        datasets: [
            {
                label: "Right",
                data: [],
                fill: false,
                borderColor: 'rgb(75, 192, 192)'
            },
            {
                label: "Left",
                data: [],
                fill: false,
                borderColor: 'rgb(192, 75, 75)'
            }
        ]
    }
});

const websocket = new WebSocket("ws://localhost:8080");

websocket.addEventListener("close", _ => {
    window.close();
});

websocket.addEventListener("message", e => {
    const data = JSON.parse(e.data);

    if (data.type == "location") {
        locationChart.data.labels.push(data.time);
        locationChart.data.datasets[0].data.push(data.value);
        locationChart.update();
    }
    else if (data.type == "speed") {
        speedChart.data.labels.push(data.time);
        speedChart.data.datasets[0].data.push(data.value.right);
        speedChart.data.datasets[1].data.push(data.value.left);
        speedChart.update();
    }
});

const inputNames = [
    ["base-speed", "baseSpeed"],
    ["target-x", "targetX"],
    ["p-sensitivity", "pSensitivity"],
    ["i-sensitivity", "iSensitivity"],
    ["d-sensitivity", "dSensitivity"],
    ["look-ahead-sensitivity-dropoff", "lookAheadSensitivityDropoff"]
];

inputNames.forEach(([name, key]) => {
    const input = document.getElementById(name);

    input.addEventListener("input", e => {
        websocket.send(JSON.stringify({
            type: key,
            value: parseFloat(e.target.value)
        }));
    });
});
