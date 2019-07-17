let wsocket;
let container;
let scene;
let camera;
let controls;
let mesh;
let mousePressed = false;
let startingDistance;

let dataStruct = {
    az: 0, // azimuth 
    el: 0, // elevation
    z: 1 // zoom
};

function initThreeJS (){
    container = document.querySelector( '#scene-container' );

    // scene
    scene = new THREE.Scene();
    scene.background = new THREE.Color( 0x7FBCDD );
    // camera
    camera = new THREE.PerspectiveCamera(35, container.clientWidth / container.clientHeight, 0.1, 100,);
    camera.position.set( 0, 0, 5 );
    // controls
    controls = new THREE.OrbitControls( camera, container );
    // mesh
    const geometry = new THREE.BoxBufferGeometry( 1, 1, 1 );
    const material = new THREE.MeshStandardMaterial( { color: 0x005000 } );
    mesh = new THREE.Mesh( geometry, material );
    scene.add( mesh );
    // light
    const ambientLight = new THREE.HemisphereLight(0xffaaff, 0x202020, 6);
    const mainLight = new THREE.DirectionalLight( 0xffffff, 4 );
    mainLight.position.set( 10, 10, 10 );
    scene.add( ambientLight, mainLight );
    // renderer
    renderer = new THREE.WebGLRenderer( { antialias: true } );
    renderer.setSize( container.clientWidth, container.clientHeight );
    renderer.setPixelRatio( window.devicePixelRatio );
    container.appendChild( renderer.domElement );

    // start the animation loop
    renderer.setAnimationLoop( () => {
        renderer.render( scene, camera );
    } );
}

function initMouseEvents (){
    container = document.querySelector( '#scene-container' );
    container.onmousemove = function(){
        if(mousePressed){
            dataStruct.az = controls.getAzimuthalAngle();
            dataStruct.el = controls.getPolarAngle();
            sendData();
        }
    };
    container.onmousedown = function(){
        mousePressed = true;
    }
    container.onmouseup = function(){
        mousePressed = false;
    }
    // mouse wheel
    container.addEventListener("wheel", function(){
        dataStruct.z = startingDistance/controls.target.distanceTo( controls.object.position );
        sendData();
    });
        
    startingDistance = controls.target.distanceTo( controls.object.position );
}

function initWebSocket (addr){
    wsocket = new WebSocket(addr);
    // set callbacks
    wsocket.onopen = () => {
        console.log("websocket onopen");
        $.notify("Connected to server", "success");
    }
    wsocket.onerror = error => {
        console.log(error);
        $.notify("WebSocket Error", "error");
    }
    wsocket.onclose = e => {
        console.log(e);
        let msg = e.reason ? "connection closed with the reason: " + e.reason : "connection closed without any reason";
        $.notify(msg, "warn");
    }
    wsocket.onmessage = (e) => {
        console.log("message received from server");
        console.log(e.data);
    }
}

function sendData(azimuth, elevation){
    if(wsocket){
        if(wsocket.readyState === WebSocket.OPEN){
            wsocket.send(JSON.stringify(dataStruct));
        }
    }
}

$(document).ready(function () {
    // more here: https://notifyjs.jpillora.com
    $.notify.defaults( {globalPosition: "top left"} );

    let btnConnect = document.querySelector( '#btn-connect' );
    btnConnect.onclick = function(){
        let addr = document.getElementById("ws-address").value;
        initWebSocket("ws://" + addr);
    }

    initThreeJS();
    initMouseEvents();
});