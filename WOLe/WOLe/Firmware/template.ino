/************************************************************
 *  WOL-e ESP32 Firmware (Power + Enhanced Devices)
 ************************************************************/

#include <WiFi.h>
#include <WiFiUdp.h>
#include <SinricPro.h>
#include <SinricProSwitch.h>
#include <Adafruit_NeoPixel.h>

/************************************************************
 *  PLACEHOLDERS (Provisioning tool fills these)
 ************************************************************/
const char* WIFI_SSID     = "{{WIFI_SSID}}";
const char* WIFI_PASSWORD = "{{WIFI_PASSWORD}}";

const char* APP_KEY       = "{{APP_KEY}}";
const char* APP_SECRET    = "{{APP_SECRET}}";

const char* SHUTDOWN_SECRET = "{{SHUTDOWN_SECRET}}";

int PC_COUNT = {{PC_COUNT}};

/************************************************************
 *  PC DATA ARRAYS (Provisioning tool fills these)
 ************************************************************/
String pcPowerDeviceIDs[5] = {
  "{{PC1_POWER_DEVICE_ID}}",
  "{{PC2_POWER_DEVICE_ID}}",
  "{{PC3_POWER_DEVICE_ID}}",
  "{{PC4_POWER_DEVICE_ID}}",
  "{{PC5_POWER_DEVICE_ID}}"
};

const int MAX_ENHANCED_DEVICE_COUNT = 20;
int ENHANCED_DEVICE_COUNT = {{ENHANCED_DEVICE_COUNT}};

String enhancedDeviceIDs[MAX_ENHANCED_DEVICE_COUNT] = {
  {{ENHANCED_DEVICE_IDS}}
};

String pcMacs[5] = {
  "{{PC1_MAC}}",
  "{{PC2_MAC}}",
  "{{PC3_MAC}}",
  "{{PC4_MAC}}",
  "{{PC5_MAC}}"
};

String pcShutdownURLs[5] = {
  "{{PC1_SHUTDOWN_URL}}",
  "{{PC2_SHUTDOWN_URL}}",
  "{{PC3_SHUTDOWN_URL}}",
  "{{PC4_SHUTDOWN_URL}}",
  "{{PC5_SHUTDOWN_URL}}"
};

/************************************************************
 *  PER-PC ACTION SWITCH BEHAVIOUR
 ************************************************************/
int enhancedDevicePcIndexes[MAX_ENHANCED_DEVICE_COUNT] = {
  {{ENHANCED_DEVICE_PC_INDEXES}}
};

String enhancedActionOn[MAX_ENHANCED_DEVICE_COUNT]  = {
  {{ENHANCED_ACTION_ON}}
};

String enhancedActionOff[MAX_ENHANCED_DEVICE_COUNT] = {
  {{ENHANCED_ACTION_OFF}}
};

/************************************************************
 *  RGB LED (WS2812 on GPIO 48)
 ************************************************************/
#define LED_PIN   48
#define LED_COUNT 1

Adafruit_NeoPixel led(LED_COUNT, LED_PIN, NEO_GRB + NEO_KHZ800);

enum LedState {
  LED_OFF,
  LED_BREATH_BLUE,
  LED_SOLID_BLUE,
  LED_FLASH_GREEN,
  LED_FLASH_RED,
  LED_FLASH_YELLOW,
  LED_FLASH_WHITE,
  LED_FLASH_PINK,
  LED_FLASH_ORANGE,
  LED_FLASH_PURPLE,
  LED_FLASH_CYAN,
  LED_FLASH_RAINBOW
};

LedState baseState    = LED_BREATH_BLUE;
LedState currentState = LED_BREATH_BLUE;

unsigned long lastLedUpdate    = 0;
const unsigned long ledInterval = 20;

float breathPhase = 0.0f;
const float breathStep = 0.05f;

unsigned long flashEndTime     = 0;
unsigned long flashStartTime   = 0;
unsigned long flashDuration    = 300;
const unsigned long launchAppRainbowDuration = 2000;

bool isFlashState(LedState state) {
  return state == LED_FLASH_GREEN  ||
         state == LED_FLASH_RED    ||
         state == LED_FLASH_YELLOW ||
         state == LED_FLASH_WHITE  ||
         state == LED_FLASH_PINK   ||
         state == LED_FLASH_ORANGE ||
         state == LED_FLASH_PURPLE ||
         state == LED_FLASH_CYAN   ||
         state == LED_FLASH_RAINBOW;
}

void setLED(uint8_t r, uint8_t g, uint8_t b) {
  led.setPixelColor(0, led.Color(r, g, b));
  led.show();
}

void setBaseState(LedState state) {
  baseState = state;
  if (!isFlashState(currentState))
    currentState = baseState;
}

void triggerFlash(LedState flashState) {
  currentState = flashState;
  flashStartTime = millis();
  flashDuration = (flashState == LED_FLASH_RAINBOW) ? launchAppRainbowDuration : 300;
  flashEndTime = flashStartTime + flashDuration;
}

void updateLed() {
  unsigned long now = millis();

  if (isFlashState(currentState) && now >= flashEndTime) {
    currentState = baseState;
  }

  if (now - lastLedUpdate < ledInterval) return;
  lastLedUpdate = now;

  switch (currentState) {
    case LED_OFF:         setLED(0,0,0); break;
    case LED_SOLID_BLUE:  setLED(0,0,255); break;

    case LED_BREATH_BLUE: {
      breathPhase += breathStep;
      if (breathPhase > 2.0f * PI) breathPhase -= 2.0f * PI;
      float normalized = (sin(breathPhase) + 1.0f) * 0.5f;
      uint8_t brightness = (uint8_t)(normalized * 255.0f);
      setLED(0,0,brightness);
      break;
    }

    case LED_FLASH_GREEN:  setLED(0,255,0); break;
    case LED_FLASH_RED:    setLED(255,0,0); break;
    case LED_FLASH_YELLOW: setLED(255,255,50); break;
    case LED_FLASH_WHITE:  setLED(255,255,255); break;
    case LED_FLASH_PINK:   setLED(255,0,128); break;
    case LED_FLASH_ORANGE: setLED(255,100,0); break;
    case LED_FLASH_PURPLE: setLED(200,0,255); break;
    case LED_FLASH_CYAN:   setLED(0,255,200); break;
    case LED_FLASH_RAINBOW: {
      float progress = (float)(now - flashStartTime) / (float)launchAppRainbowDuration;
      if (progress < 0.0f) progress = 0.0f;
      if (progress > 1.0f) progress = 1.0f;
      uint16_t hue = (uint16_t)(progress * 65535.0f);
      led.setPixelColor(0, led.gamma32(led.ColorHSV(hue, 255, 255)));
      led.show();
      break;
    }
  }
}

/************************************************************
 *  WOL
 ************************************************************/
WiFiUDP UDP;

void sendWOL(const char* macStr) {
  byte mac[6];
  sscanf(macStr, "%hhx:%hhx:%hhx:%hhx:%hhx:%hhx",
         &mac[0], &mac[1], &mac[2], &mac[3], &mac[4], &mac[5]);

  byte packet[102];
  memset(packet, 0xFF, 6);
  for (int i = 6; i < 102; i += 6)
    memcpy(packet + i, mac, 6);

  UDP.beginPacket("255.255.255.255", 9);
  UDP.write(packet, 102);
  UDP.endPacket();
}

/************************************************************
 *  HTTP ACTION HELPERS
 ************************************************************/
void sendActionRequest(String baseUrl, String action, LedState flashColor) {
  WiFiClient client;

  String url = baseUrl + "/" + action + "?key=" + SHUTDOWN_SECRET;

  Serial.println("--------------------------------------------------");
  Serial.println("Action: " + action);
  Serial.println("URL: " + url);

  String host = baseUrl;
  host.replace("http://", "");
  int colonIndex = host.indexOf(':');
  int port = 80;

  if (colonIndex > 0) {
    port = host.substring(colonIndex + 1).toInt();
    host = host.substring(0, colonIndex);
  }

  Serial.println("Host: " + host);
  Serial.println("Port: " + String(port));

  if (client.connect(host.c_str(), port)) {
    client.print(String("GET /") + action + "?key=" + SHUTDOWN_SECRET +
                 " HTTP/1.1\r\nHost: " + host +
                 "\r\nConnection: close\r\n\r\n");
    Serial.println("Request sent.");
  } else {
    Serial.println("Connection FAILED.");
  }

  client.stop();
  triggerFlash(flashColor);
}

/************************************************************
 *  POWER SWITCH SEQUENCE DETECTION (Switch A)
 ************************************************************/
String powerSeq = "";
unsigned long powerLastEventTime = 0;
const unsigned long powerSeqTimeout = 1000;

// ⭐ NEW: Track which PC triggered the power event
int currentPowerPcIndex = -1;

void addPowerEvent(bool state) {
  unsigned long now = millis();

  if (now - powerLastEventTime > powerSeqTimeout) {
    powerSeq = "";
  }

  powerLastEventTime = now;
  powerSeq += (state ? "1" : "0");

  Serial.println("POWER SEQ = " + powerSeq);
}

void evaluatePowerSequence() {
  if (powerSeq == "") return;

  unsigned long now = millis();
  if (now - powerLastEventTime < powerSeqTimeout) return;

  String s = powerSeq;
  powerSeq = "";

  Serial.println("FINAL POWER SEQ = " + s);

  // ⭐ NEW: Use the correct PC index
  if (currentPowerPcIndex < 0 || currentPowerPcIndex >= PC_COUNT) {
    Serial.println("Invalid PC index for power event.");
    return;
  }

  int i = currentPowerPcIndex;

  if (s == "1") {
    sendWOL(pcMacs[i].c_str());
    triggerFlash(LED_FLASH_GREEN);
    return;
  }

  if (s == "0") {
    sendActionRequest(pcShutdownURLs[i], "shutdown", LED_FLASH_RED);
    return;
  }

  if (s == "01") {
    sendActionRequest(pcShutdownURLs[i], "restart", LED_FLASH_YELLOW);
    return;
  }
}

/************************************************************
 *  ACTION SWITCH HANDLER (Switch B)
 ************************************************************/
LedState getFlashColorForAction(String action) {
  if (action == "restart")    return LED_FLASH_YELLOW;
  if (action == "sleep")      return LED_FLASH_PINK;
  if (action == "hibernate")  return LED_FLASH_WHITE;
  if (action == "lock")       return LED_FLASH_ORANGE;
  if (action == "screenoff")  return LED_FLASH_PURPLE;
  if (action == "launchapp1") return LED_FLASH_RAINBOW;
  if (action == "launchapp2") return LED_FLASH_RAINBOW;
  if (action == "launchapp3") return LED_FLASH_RAINBOW;
  if (action == "launchapp4") return LED_FLASH_RAINBOW;

  return LED_FLASH_WHITE;
}

void handleActionSwitch(const String &deviceId, bool state) {
  for (int i = 0; i < ENHANCED_DEVICE_COUNT; i++) {
    if (deviceId == enhancedDeviceIDs[i]) {
      int pcIndex = enhancedDevicePcIndexes[i];
      if (pcIndex < 0 || pcIndex >= PC_COUNT) continue;

      String action = state ? enhancedActionOn[i] : enhancedActionOff[i];
      LedState color = getFlashColorForAction(action);

      Serial.println("Action switch [PC" + String(pcIndex + 1) + "] -> " + action);
      sendActionRequest(pcShutdownURLs[pcIndex], action, color);
      return;
    }
  }
}

/************************************************************
 *  SINRIC CALLBACKS
 ************************************************************/
bool onPowerState(const String &deviceId, bool state) {

  // ⭐ NEW: Identify which PC this power switch belongs to
  for (int i = 0; i < PC_COUNT; i++) {
    if (deviceId == pcPowerDeviceIDs[i]) {
      currentPowerPcIndex = i;
      addPowerEvent(state);
      return true;
    }
  }

  handleActionSwitch(deviceId, state);
  return true;
}

/************************************************************
 *  SINRIC SETUP
 ************************************************************/
void setupSinric() {
  for (int i = 0; i < PC_COUNT; i++) {
    if (pcPowerDeviceIDs[i].length() > 5) {
      SinricProSwitch& swPower = SinricPro[pcPowerDeviceIDs[i]];
      swPower.onPowerState(onPowerState);
    }
  }

  for (int i = 0; i < ENHANCED_DEVICE_COUNT; i++) {
    if (enhancedDeviceIDs[i].length() > 5) {
      SinricProSwitch& swAction = SinricPro[enhancedDeviceIDs[i]];
      swAction.onPowerState(onPowerState);
    }
  }

  SinricPro.onConnected([]() {
    Serial.println("Connected to SinricPro");
    setBaseState(LED_SOLID_BLUE);
  });

  SinricPro.onDisconnected([]() {
    Serial.println("Disconnected from SinricPro");
    setBaseState(LED_BREATH_BLUE);
  });

  SinricPro.begin(APP_KEY, APP_SECRET);
}

/************************************************************
 *  SETUP
 ************************************************************/
void setup() {
  Serial.begin(115200);
  delay(300);

  led.begin();
  led.setBrightness(255);
  setBaseState(LED_BREATH_BLUE);
  updateLed();

  Serial.println("Connecting to WiFi...");
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  while (WiFi.status() != WL_CONNECTED) {
    updateLed();
    delay(10);
  }

  Serial.println("WiFi connected");
  Serial.print("ESP32 IP Address: ");
  Serial.println(WiFi.localIP());

  UDP.begin(9);
  setupSinric();
}

/************************************************************
 *  LOOP
 ************************************************************/
void loop() {
  if (WiFi.status() != WL_CONNECTED)
    setBaseState(LED_BREATH_BLUE);
  else if (!isFlashState(currentState))
    setBaseState(LED_SOLID_BLUE);

  updateLed();
  SinricPro.handle();
  evaluatePowerSequence();
}
