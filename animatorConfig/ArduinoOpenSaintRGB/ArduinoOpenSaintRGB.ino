#include <Adafruit_NeoPixel.h>
#include <ctype.h>
 
#define NUM_LEDS 16  // Assume typicaly 16 pin ring 

#define LEFTEYE_PIN 7 // Arduino pin out may vary
#define LEFTVENT_PIN 8
#define RIGHTEYE_PIN 9
#define RIGHTVENT_PIN 10

// Set 4 lights
Adafruit_NeoPixel leftEye(NUM_LEDS, LEFTEYE_PIN, NEO_RGB + NEO_KHZ800);
Adafruit_NeoPixel leftVent(NUM_LEDS, LEFTVENT_PIN, NEO_RGB + NEO_KHZ800);
Adafruit_NeoPixel rightEye(NUM_LEDS, RIGHTEYE_PIN, NEO_RGB + NEO_KHZ800);
Adafruit_NeoPixel rightVent(NUM_LEDS, RIGHTVENT_PIN, NEO_RGB + NEO_KHZ800);

//******** Helper methods ******************

// Splits Strings into any number of arguments
int splitStringAuto(String input, char delimiter, String** outParts) {
  int count = 1;
  for (int i = 0; i < input.length(); i++) {
    if (input[i] == delimiter) count++;
  }

  *outParts = new String[count];  // dynamically allocate the array

  int tokenIndex = 0;
  int startIndex = 0;
  int delimiterIndex = input.indexOf(delimiter);

  while (delimiterIndex != -1) {
    (*outParts)[tokenIndex++] = input.substring(startIndex, delimiterIndex);
    startIndex = delimiterIndex + 1;
    delimiterIndex = input.indexOf(delimiter, startIndex);
  }

  (*outParts)[tokenIndex++] = input.substring(startIndex); // last token
  return count;
}

// Input a value 0 to 255 to get a color value.
// The colours are a transition r - g - b - back to r.
uint32_t Wheel(byte WheelPos) {
  WheelPos = 255 - WheelPos;
  if(WheelPos < 85) {
    leftEye.Color(255 - WheelPos * 3, 0, WheelPos * 3);
    return leftEye.Color(255 - WheelPos * 3, 0, WheelPos * 3);
  }
  if(WheelPos < 170) {
    WheelPos -= 85;
    leftEye.Color(0, WheelPos * 3, 255 - WheelPos * 3);
    return leftEye.Color(0, WheelPos * 3, 255 - WheelPos * 3);
  }
  WheelPos -= 170;
  leftEye.Color(WheelPos * 3, 255 - WheelPos * 3, 0);
  return leftEye.Color(WheelPos * 3, 255 - WheelPos * 3, 0);
}

// **************** end helper methods ***********************

// Ring light effects

void SetColorAll(uint32_t allColor, int brightness, bool eye, bool vent, bool left, bool right)
{ 
    if(eye)
    {
    if(left) leftEye.setBrightness(brightness);
    if(right)rightEye.setBrightness(brightness);
    }
    if(vent)
    {
    if(left)leftVent.setBrightness(brightness);
    if(right)rightVent.setBrightness(brightness);
    }

    for(int i=0; i< NUM_LEDS; i++)
      {
        if(eye)
        {
         if(left)leftEye.setPixelColor(i, allColor); 
         if(right)rightEye.setPixelColor(i, allColor); 
        }
        if(vent)
        {
         if(left)leftVent.setPixelColor(i, allColor); 
         if(right)rightVent.setPixelColor(i, allColor);
        }
      }

    if(eye) 
    {
    if(left)leftEye.show();
    if(right)rightEye.show();
    }
    if(vent)
    {
    if(left)leftVent.show();
    if(right)rightVent.show();
    }
}


unsigned long PulseColorAllNonBlocking(uint32_t allColor, int brightness, bool eye, bool vent, bool left, bool right, uint16_t delayms, uint16_t pulses,int brightnessStep)
{
  // Persistent state between calls
  static int level = 0;
  static int pulse = 0;
  static int direction = 1;  // 1 = up, -1 = down
  static unsigned long nextRun = 0;

  unsigned long now = millis();

  // First call initialization
  if (nextRun == 0 && pulse == 0 && level == 0) {
    direction = 1;
    nextRun = now;
  }

  // Only proceed when it's time
  if (now < nextRun)
    return nextRun;

  // --- Update LED brightness ---
  if (eye) {
    if (left)  leftEye.setBrightness(level);
    if (right) rightEye.setBrightness(level);
  }
  if (vent) {
    if (left)  leftVent.setBrightness(level);
    if (right) rightVent.setBrightness(level);
  }

  // --- Set all pixels to the same color ---
  for (int i = 0; i < NUM_LEDS; i++) {
    if (eye) {
      if (left)  leftEye.setPixelColor(i, allColor);
      if (right) rightEye.setPixelColor(i, allColor);
    }
    if (vent) {
      if (left)  leftVent.setPixelColor(i, allColor);
      if (right) rightVent.setPixelColor(i, allColor);
    }
  }

  // --- Show updated brightness ---
  if (eye) {
    if (left)  leftEye.show();
    if (right) rightEye.show();
  }
  if (vent) {
    if (left)  leftVent.show();
    if (right) rightVent.show();
  }

  // --- Update brightness level ---
  level += (brightnessStep * direction);

  // Reverse direction at limits
  if (level >= brightness) {
    level = brightness;
    direction = -1;
  } else if (level <= 0) {
    level = 0;
    direction = 1;
    pulse++;

    // Check if finished all pulses
    if (pulse >= pulses) {
      // Reset and clear LEDs
      pulse = 0;
      level = 0;
      direction = 1;
      nextRun = 0;

      SetColorAll(leftEye.Color(0, 0, 0), 0, eye, vent, left, right);
      return 0; // done
    }
  }

  // Schedule next update
  nextRun = now + delayms;
  return nextRun;
}

unsigned long FadeColorNonBlocking(uint32_t allColor, int highestBrightness,  bool eye, bool vent, bool left, bool right,  uint16_t delayms,  bool fadeIn, uint16_t brightnessStep, uint16_t lowestBrightness)
{
  // Persistent state
  static int level = 0;
  static int direction = 0;
  static bool initialized = false;
  static unsigned long nextRun = 0;

  unsigned long now = millis();

  // --- Initialization (first call) ---
  if (!initialized) {
    level = fadeIn ? lowestBrightness : highestBrightness;
    direction = fadeIn ? brightnessStep : -brightnessStep;
    nextRun = now;
    initialized = true;
  }

  // Wait until it's time to update
  if (now < nextRun)
    return nextRun;

  // --- Update brightness level ---
  level += direction;

  // Check for completion conditions
  if (!fadeIn && level <= lowestBrightness) {
    level = lowestBrightness;
    initialized = false;   // reset state
  }
  if (fadeIn && level >= highestBrightness) {
    level = highestBrightness;
    initialized = false;   // reset state
  }

  // --- Apply brightness to eyes/vents ---
  if (eye) {
    if (left)  leftEye.setBrightness(level);
    if (right) rightEye.setBrightness(level);
  }
  if (vent) {
    if (left)  leftVent.setBrightness(level);
    if (right) rightVent.setBrightness(level);
  }

  // --- Set all pixels to target color ---
  for (int i = 0; i < NUM_LEDS; i++) {
    if (eye) {
      if (left)  leftEye.setPixelColor(i, allColor);
      if (right) rightEye.setPixelColor(i, allColor);
    }
    if (vent) {
      if (left)  leftVent.setPixelColor(i, allColor);
      if (right) rightVent.setPixelColor(i, allColor);
    }
  }

  // --- Show the updated colors ---
  if (eye) {
    if (left)  leftEye.show();
    if (right) rightEye.show();
  }
  if (vent) {
    if (left)  leftVent.show();
    if (right) rightVent.show();
  }

  // --- Schedule next update or finish ---
  if (!initialized) {
    nextRun = 0;      
    return 0;
  } else {
    nextRun = now + delayms;
    return nextRun;
  }
}

unsigned long ColorWipeEyesNonBlocking(uint32_t color, uint16_t brightness, bool left, bool right, uint16_t wait)
{
  // Persistent state between calls
  static uint16_t i = 0;             // current pixel index
  static unsigned long nextRun = 0;  // next scheduled time
  static bool initialized = false;

  unsigned long now = millis();

  // --- Initialization (first call) ---
  if (!initialized) {
    if (left)  leftEye.setBrightness(brightness);
    if (right) rightEye.setBrightness(brightness);
    i = 0;
    nextRun = now;
    initialized = true;
  }

  // --- Wait until it’s time for next pixel ---
  if (now < nextRun)
    return nextRun;

  // --- Update one pixel ---
  if (left) {
    leftEye.setPixelColor(i, color);
    leftEye.show();
  }
  if (right) {
    rightEye.setPixelColor(i, color);
    rightEye.show();
  }

  // --- Advance to next pixel ---
  i++;

  // --- Check if done ---
  if (i >= leftEye.numPixels()) {
    // Reset state for next time
    i = 0;
    initialized = false;
    nextRun = 0;
     SetColorAll(leftEye.Color(0, 0, 0), 0, true, true, true, true);
    return 0; // finished
  }

  // --- Schedule next update ---
  nextRun = now + wait;
  return nextRun;
}

unsigned long TheaterChaseNonBlocking(uint32_t c, uint16_t brightness, bool left, bool right, uint16_t delayms, uint16_t cycles)
{
  static uint16_t j = 0;      // current cycle
  static uint8_t q = 0;       // inner offset (0–2)
  static unsigned long nextRun = 0; // next time to run

  // current time
  unsigned long now = millis();

  // first call — initialize
  if (j == 0 && q == 0 && nextRun == 0) {
    if (left) leftEye.setBrightness(brightness);
    if (right) rightEye.setBrightness(brightness);
    nextRun = now;
  }

  // only proceed if enough time has passed
  if (now < nextRun) return nextRun;

  // ---- perform one step of the animation ----
  // turn on every third pixel at offset q
  for (uint16_t i = 0; i < leftEye.numPixels(); i += 3) {
    if (left)  leftEye.setPixelColor(i + q, c);
    if (right) rightEye.setPixelColor(i + q, c);
  }

  // show the pattern
  if (left) leftEye.show();
  if (right) rightEye.show();

  // turn those pixels back off for next time
  for (uint16_t i = 0; i < leftEye.numPixels(); i += 3) {
    if (left)  leftEye.setPixelColor(i + q, 0);
    if (right) rightEye.setPixelColor(i + q, 0);
  }

  // advance animation state
  q++;
  if (q >= 3) {   // inner loop complete
    q = 0;
    j++;
    if (j >= cycles) {
      // finished full animation
      j = 0;
      nextRun = 0;
       SetColorAll(leftEye.Color(0,0,0), 0, true, true, true, true);    
      return 0;  // signal done
    }
  }

  // schedule next run
  nextRun = now + delayms;
  return nextRun;
}

unsigned long CylonNonBlocking(uint16_t delayms, uint16_t cycles)
{
  // --- Persistent state ---
  static unsigned long nextRun = 0;
  static bool initialized = false;
  static uint16_t cycleCount = 0;
  static int phase = 0;  // 0=right fwd,1=left fwd,2=left rev,3=right rev
  static int q = 0;      // position index within phase

  // constants
  const uint16_t leftOffset = 3;
  const uint16_t rightOffset = 2;
  const uint8_t span = 7;
  const uint16_t brightness = 200;

  unsigned long now = millis();

  // detect new start (called after completion or re-start)
  if (!initialized) {
    leftEye.setBrightness(brightness);
    rightEye.setBrightness(brightness);
    cycleCount = 0;
    phase = 0;
    q = 0;
    nextRun = now;
    initialized = true;
  }

  // wait until time to run next step
  if (now < nextRun)
    return nextRun;

  uint32_t red = leftEye.Color(0, 255, 0);

  // --- Step logic ---
  switch (phase)
  {
    case 0: // Right eye: forward
      rightEye.setPixelColor(q + rightOffset, red);
      if (q > 0) rightEye.setPixelColor(q + rightOffset - 1, 0);
      rightEye.show();
      q++;
      if (q >= span) {
        rightEye.setPixelColor(span - 1 + rightOffset, 0);
        rightEye.show();
        q = 0;
        phase++;
      }
      break;

    case 1: // Left eye: forward
      leftEye.setPixelColor(q + leftOffset, red);
      if (q > 0) leftEye.setPixelColor(q + leftOffset - 1, 0);
      leftEye.show();
      q++;
      if (q >= span) {
        leftEye.setPixelColor(span - 1 + leftOffset, 0);
        leftEye.show();
        q = span - 1;
        phase++;
      }
      break;

    case 2: // Left eye: reverse
      leftEye.setPixelColor(q + leftOffset, red);
      leftEye.setPixelColor(q + leftOffset + 1, 0);
      leftEye.show();
      q--;
      if (q < 0) {
        leftEye.setPixelColor(leftOffset, 0);
        leftEye.show();
        q = span - 1;
        phase++;
      }
      break;

    case 3: // Right eye: reverse
      rightEye.setPixelColor(q + rightOffset, red);
      rightEye.setPixelColor(q + rightOffset + 1, 0);
      rightEye.show();
      q--;
      if (q < 0) {
        rightEye.setPixelColor(rightOffset, 0);
        rightEye.show();
        q = 0;
        phase = 0;
        cycleCount++;
        if (cycleCount >= cycles) {
          SetColorAll(leftEye.Color(0, 0, 0), 0, true, true, true, true);
          initialized = false;     // reset for next start
          nextRun = 0;
          return 0;                // signal finished
        }
      }
      break;
  }

  nextRun = now + delayms;
  return nextRun;
}

unsigned long RainbowNonBlocking(uint16_t brightness,  bool left, bool right, uint8_t delayms)
{
  // --- Persistent state ---
  static unsigned long nextRun = 0;
  static uint16_t j = 0;          // color wheel index
  static bool initialized = false;
  static bool finished = false;   // flag to track completion
  unsigned long now = millis();

  // --- Detect restart or new call ---
  if (!initialized || finished) {
    if (left)  leftEye.setBrightness(brightness);
    if (right) rightEye.setBrightness(brightness);
    j = 0;
    nextRun = now;
    initialized = true;
    finished = false;
  }

  // --- Wait until it's time to step ---
  if (now < nextRun)
    return nextRun;

  // --- Update LEDs for this frame ---
  for (uint16_t i = 0; i < leftEye.numPixels(); i++) {
    uint8_t colorIndex = (i + j) & 255;
    if (left)  leftEye.setPixelColor(i, Wheel(colorIndex));
    if (right) rightEye.setPixelColor(i, Wheel(colorIndex));
  }

  if (left)  leftEye.show();
  if (right) rightEye.show();

  // --- Advance color wheel ---
  j++;

  // --- Check if finished ---
  if (j >= 256) {
    // Clean up and mark as complete
    j = 0;
    nextRun = 0;
    finished = true;
    initialized = false;
    return 0;   // signal animation finished
  }

  // --- Schedule next frame ---
  nextRun = now + delayms;
  return nextRun;
}
unsigned long RainbowCycleNonBlocking(uint16_t brightness, bool left, bool right, uint8_t delayms, uint8_t cycles)
{
  // --- Persistent state ---
  static unsigned long nextRun = 0;
  static uint16_t j = 0;          // color wheel index
  static bool initialized = false;
  static bool finished = false;
  unsigned long now = millis();

  // --- Detect restart or new start ---
  if (!initialized || finished) {
    if (left)  leftEye.setBrightness(brightness);
    if (right) rightEye.setBrightness(brightness);
    j = 0;
    nextRun = now;
    initialized = true;
    finished = false;
  }

  // --- Wait until next step ---
  if (now < nextRun)
    return nextRun;

  // --- Frame update ---
  uint16_t num = leftEye.numPixels();
  for (uint16_t i = 0; i < num; i++) {
    uint8_t colorIndex = ((i * 256 / num) + j) & 255;
    if (left)  leftEye.setPixelColor(i, Wheel(colorIndex));
    if (right) rightEye.setPixelColor(i, Wheel(colorIndex));
  }

  if (left)  leftEye.show();
  if (right) rightEye.show();

  // --- Advance rainbow index ---
  j++;

  // --- Check completion (256 * cycles frames) ---
  if (j >= 256 * cycles) {
    j = 0;
    nextRun = 0;
    finished = true;
    initialized = false;
     SetColorAll(leftEye.Color(0, 0, 0), 0, true, true, true, true);
    return 0;   // done
  }

  // --- Schedule next run ---
  nextRun = now + delayms;
  return nextRun;
}

unsigned long RainbowTheaterChaseNonBlocking(uint16_t brightness, bool left, bool right, uint8_t delayms)
{
  static unsigned long nextRun = 0;
  static int j = 0;  // color wheel position
  static int q = 0;  // offset 0–2
  static bool initialized = false;
  unsigned long now = millis();

  if (!initialized) {
    if (left)  leftEye.setBrightness(brightness);
    if (right) rightEye.setBrightness(brightness);
    j = 0;
    q = 0;
    nextRun = now;
    initialized = true;
  }

  if (now < nextRun)
    return nextRun;

  // --- turn every third pixel ON ---
  for (uint16_t i = 0; i < NUM_LEDS; i += 3) {
    uint8_t colorIndex = (i + j) % 255;
    if (left)  leftEye.setPixelColor(i + q, Wheel(colorIndex));
    if (right) rightEye.setPixelColor(i + q, Wheel(colorIndex));
  }

  if (left)  leftEye.show();
  if (right) rightEye.show();

  // --- turn them OFF again for next phase ---
  for (uint16_t i = 0; i < NUM_LEDS; i += 3) {
    if (left)  leftEye.setPixelColor(i + q, 0);
    if (right) rightEye.setPixelColor(i + q, 0);
  }

  // advance state
  q++;
  if (q >= 3) {
    q = 0;
    j++;
    if (j >= 256) {
      j = 0;
      initialized = false;
      nextRun = 0;
      return 0; // done
    }
  }

  nextRun = now + delayms;
  return nextRun;
}

// Used for readin commands 
String inputBuffer = "";
bool lineReady = false;

    bool left = true;
    bool right = true;
    bool eyes = true;
    bool vents = true;
    bool fadeIn = true;
    uint16_t delayms = 40;
    uint16_t pulses = 3;
    uint16_t step = 1;
    uint16_t brightness = 200;
    uint16_t lowestBrightness = 0;
    String eyesVents = "";
    String whichEye = "";
    uint32_t allColor = 0; 
    uint16_t commandIndex = 0;
    String Commands[4] = {"","","",""};
    unsigned long TriggerTimes[4] =  {0,0,0,0};


void RunExistingCommands()
{
   unsigned long now = millis();
   
  for(uint16_t i =0; i< 4; i++)
  {
   if(TriggerTimes[i] != 0 && now > TriggerTimes[i])
   {
    if(Commands[i] == "CylonNonBlocking")
    {    
      TriggerTimes[i] = CylonNonBlocking( delayms, pulses);                 
    }
     else if(Commands[i] == "TheaterChaseNonBlocking")
    {    
      TriggerTimes[i] = TheaterChaseNonBlocking(allColor, brightness, left, right, delayms, pulses);  
    }
    else if(Commands[i] == "PulseColorAllNonBlocking")
    {    
      TriggerTimes[i] = PulseColorAllNonBlocking(allColor, brightness, eyes,vents, left, right, delayms, pulses, step); 
    }
     else if(Commands[i] == "FadeColorNonBlocking")
    {    
   TriggerTimes[i] = FadeColorNonBlocking( allColor, brightness, eyes, vents, left, right, delayms, fadeIn, step, lowestBrightness);       
    }
     else if(Commands[i] == "ColorWipeEyesNonBlocking")
    {    
      TriggerTimes[i] = ColorWipeEyesNonBlocking( allColor, brightness, left, right, delayms);                 
    }   
      else if(Commands[i] == "RainbowNonBlocking")
    {    
         TriggerTimes[i] = RainbowNonBlocking(brightness, left, right, delayms); 
    }
    else if(Commands[i] == "RainbowCycleNonBlocking")
    {    
         TriggerTimes[i] = RainbowCycleNonBlocking(brightness, left, right, delayms, 3);  
    }
      else if(Commands[i] == "RainbowTheaterChaseNonBlocking")
    {    
        TriggerTimes[i] = RainbowTheaterChaseNonBlocking(brightness, left, right, delayms);  
    }
   }
  }
}

void MainParsing()
  {
    left = true;
    right = true;
    eyes = true;
    vents = true;
    fadeIn = true;
    delayms = 40;
    pulses = 3;
    step = 1;
    brightness = 200;
    lowestBrightness = 0;
    eyesVents = "";
    whichEye = "";
    allColor = 0; 
    commandIndex = 0;

    // Parse input string  not case sensitive, clears white space
    String* token;
    inputBuffer.toUpperCase();
    inputBuffer.replace(" ","");

    // returns argument count for error checking
    int count = splitStringAuto(inputBuffer, ',', &token);     
    
    String command = token[0];    
    
    if(command == "CYLON" )
    {
      delayms = token[1].toInt();
      pulses = token[2].toInt();
      TriggerTimes[0] = CylonNonBlocking( delayms, pulses);
      Commands[0] = "CylonNonBlocking";    
    }
    else if(command == "CLEAR" && count == 3) {      
          eyesVents = token[1];
          eyes = (eyesVents == "EYES" || eyesVents == "BOTH");
          vents = (eyesVents == "VENTS" || eyesVents == "BOTH");
          whichEye = token[2];
          left= (whichEye == "LEFT" || whichEye == "LR");
          right = (whichEye == "RIGHT" || whichEye == "LR");          
          SetColorAll(leftEye.Color(0,0,0), 0, eyes, vents, left, right);   
    }
    else if(command == "CLEARALL") 
    {     
          // for(uint16_t i =0; i< 4; i++)
          //   {
          //     Commands[i]  = "";
          //     TriggerTimes[i] = 0;
          //   }
          SetColorAll(leftEye.Color(0,0,0), 0, true, true, true, true);     
    }
    else if(count == 4 && (command == "RAINBOW" || command == "RAINBOWCYCLE" || command == "RAINBOWCHASE"))
    {
      brightness = token[1].toInt();
       whichEye = token[2];
       left = (whichEye == "LEFT" || whichEye == "LR");
       right = (whichEye == "RIGHT" || whichEye == "LR"); 
       delayms = token[3].toInt();
       
       if(command == "RAINBOW")
       {
    TriggerTimes[0] = RainbowNonBlocking(brightness, left, right, delayms);         
         Commands[0] = "RainbowNonBlocking"; 
       }
       else if(command == "RAINBOWCYCLE")
       {
         TriggerTimes[0] = RainbowCycleNonBlocking(brightness, left, right, delayms, 3); // 3 cycles       
         Commands[0] = "RainbowCycleNonBlocking";
       }
       if(command == "RAINBOWCHASE" )
       {
         TriggerTimes[0] = RainbowTheaterChaseNonBlocking(brightness, left, right, delayms);  
         Commands[0] = "RainbowTheaterChaseNonBlocking";
       }
    }    
    else if( count >= 6 ) // for commands with standard format and at least 6 arguments
    {
          // parse standard arguments
          allColor = leftEye.Color( token[2].toInt(), token[1].toInt(), token[3].toInt());     
          brightness = token[4].toInt();
          eyesVents = token[5];
          eyes = (eyesVents == "EYES" || eyesVents == "BOTH");
          vents = (eyesVents == "VENTS" || eyesVents == "BOTH");
          whichEye = token[6];
          left = (whichEye == "LEFT" || whichEye == "LR");
          right = (whichEye == "RIGHT" || whichEye == "LR");   
        

      if(command == "SETRGBCOLOR") 
      {         
        SetColorAll(allColor, brightness, eyes, vents, left, right);
      }      
      else if(command == "COLORWIPEEYES")
      {
        delayms = token[7].toInt();    
         TriggerTimes[0] = ColorWipeEyesNonBlocking( allColor, brightness, left, right, delayms); 
         Commands[0] = "ColorWipeEyesNonBlocking";  
      } 
      else if(command == "FADE") 
      { 
          delayms = token[7].toInt();        
          fadeIn = (token[8] == "IN");
          step = token[9].toInt();
          lowestBrightness = token[10].toInt(); 
          TriggerTimes[0] = FadeColorNonBlocking( allColor, brightness, eyes, vents, left, right, delayms, fadeIn, step, lowestBrightness);
          Commands[0] = "FadeColorNonBlocking"; 
      }
      else if(command == "PULSE") 
      { 
        delayms = token[7].toInt();
        pulses = token[8].toInt();
        step = token[9].toInt();       
        TriggerTimes[0] = PulseColorAllNonBlocking(allColor, brightness, eyes,vents, left, right, delayms, pulses, step); 
        Commands[0] = "PulseColorAllNonBlocking";               
      }  
      else if(command == "THEATERCHASE") // typical 40ms delay, 10 cycles
      {
        delayms = token[7].toInt();
        pulses = token[8].toInt();
        TriggerTimes[0] = TheaterChaseNonBlocking(allColor, brightness, left, right, delayms, pulses);
        Commands[0] = "TheaterChaseNonBlocking";             
      }
    }      
     // free memory so the heap doesn't fill up
     delete[] token;
  }
    

// Main running code

void setup() {
  leftEye.begin();
  leftEye.show();
  rightEye.begin();
  rightEye.show();
  leftVent.begin();
  leftVent.show();
  rightVent.begin();
  rightVent.show();

  Serial.begin(115200);
  inputBuffer.reserve(200);
}


// This gets called automatically when serial data arrives
void readSerialLine() {
  while (Serial.available() > 0) {
    char c = Serial.read();  

    if (c == '\n' || c == '\r') {
      if (inputBuffer.length() > 0) {
        lineReady = true;
        return;  // return early; we'll handle the line in loop()
      }
    } else {
      inputBuffer += c;
    }

    // limit buffer size to prevent overflow
    if (inputBuffer.length() > 199) {
      inputBuffer = "";     
    }
  }
}

// ******  Main Loop *****

 void loop()   
  {
     readSerialLine();

     if(lineReady)
     {
      MainParsing();

      inputBuffer = "";
      lineReady = false;
     }
     
     RunExistingCommands();     
  }