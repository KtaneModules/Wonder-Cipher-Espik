using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class WonderCipher : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMColorblindMode ColorblindMode;

    public KMSelectable[] Keys;
    public KMSelectable[] RelevantKeys;
    public KMSelectable DeleteKey;
    public KMSelectable SubmitButton;
    public KMSelectable ViewButton;

    public TextMesh[] ScreenText;
    public Color[] ScreenColors;

    public Renderer LightModel;
    public Light ModuleLight;
    public Material RedMaterial;
    public Color RedColor;

    public TextMesh ColorblindText;

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;

    // Solving info
    private int moduleStatus = 0;
    private int lettersEntered = 0;

    private readonly string[] availableLetters = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "B", "C", "D", "F", "G", "H",
                                                    "J", "K", "L", "M", "N", "P", "Q", "R", "S", "T", "V", "W", "X", "Y", "Z", "?" };

    private readonly string[] availableKeys = { "C", "F", "H", "J", "K", "M", "N", "P", "Q", "R", "S", "T", "W", "X", "Y",
                                                "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "@", "&", "-", "#", "%", "+", "=" };

    private Dictionary<string, KMSelectable> keysDict = new Dictionary<string, KMSelectable>();
    private string givenText = "";
    private string enteredText = "";
    private string solutionText = "";

    private string binaryString = "";
    private string hexString = "";

    private int[] charValues = new int[20];

    private bool redLight = false;

    private bool colorblindMode = false;


    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        // Delegation
        for (int i = 0; i < Keys.Length; i++) {
            int j = i;
            Keys[i].OnInteract += delegate () { KeyPressed(j); return false; };
        }

        for (int i = 0; i < RelevantKeys.Length; i++) {
            int j = i;
            RelevantKeys[i].OnInteract += delegate () { RelevantKeyPressed(j); return false; };
        }

        DeleteKey.OnInteract += delegate () { DeleteKeyPressed(); return false; };
        SubmitButton.OnInteract += delegate () { SubmitButtonPressed(); return false; };
        ViewButton.OnInteract += delegate () { ViewButtonPressed(); return false; };

        keysDict = new Dictionary<string, KMSelectable>()
        {
            {"C", RelevantKeys[0]},
            {"F", RelevantKeys[1]},
            {"H", RelevantKeys[2]},
            {"J", RelevantKeys[3]},
            {"K", RelevantKeys[4]},
            {"M", RelevantKeys[5]},
            {"N", RelevantKeys[6]},
            {"P", RelevantKeys[7]},
            {"Q", RelevantKeys[8]},
            {"R", RelevantKeys[9]},
            {"S", RelevantKeys[10]},
            {"T", RelevantKeys[11]},
            {"W", RelevantKeys[12]},
            {"X", RelevantKeys[13]},
            {"Y", RelevantKeys[14]},
            {"0", RelevantKeys[15]},
            {"1", RelevantKeys[16]},
            {"2", RelevantKeys[17]},
            {"3", RelevantKeys[18]},
            {"4", RelevantKeys[19]},
            {"5", RelevantKeys[20]},
            {"6", RelevantKeys[21]},
            {"7", RelevantKeys[22]},
            {"8", RelevantKeys[23]},
            {"9", RelevantKeys[24]},
            {"@", RelevantKeys[25]},
            {"&", RelevantKeys[26]},
            {"-", RelevantKeys[27]},
            {"#", RelevantKeys[28]},
            {"%", RelevantKeys[29]},
            {"+", RelevantKeys[30]},
            {"=", RelevantKeys[31]}
        };
    }

    // Sets up the solution and preps displays
    private void Start() {
        // Scales the module light to account for bomb size
        float scalar = transform.lossyScale.x;
        ModuleLight.range *= scalar;

        // Displays the display text on the screen
        GenerateStartingMessage();
        Debug.LogFormat("[Wonder Cipher #{0}] The given message is {1}", moduleId, FormatText(givenText));

        for (int i = 0; i < ScreenText.Length; i++) {
            ScreenText[i].text = givenText.Substring(i, 1);
        }

        // Determines if the light is blue or red
        int redLightOn = UnityEngine.Random.Range(0, 2);
        if (redLightOn == 1) {
            redLight = true;
            LightModel.material = RedMaterial;
            ModuleLight.color = RedColor;
            Debug.LogFormat("[Wonder Cipher #{0}] The light on the module is red.", moduleId);
        }

        else
            Debug.LogFormat("[Wonder Cipher #{0}] The light on the module is blue.", moduleId);

        // Colorblind mode
        colorblindMode = ColorblindMode.ColorblindModeActive;
        if (colorblindMode == true) {
            if (redLight == true)
                ColorblindText.text = "RED";

            else
                ColorblindText.text = "BLUE";
        }

        ConvertToHex();
        SwapHexPairs();
        ConvertToBase32();
        CharSwap();
        TranslateToMessage();
        Debug.LogFormat("[Wonder Cipher #{0}] The solution message is {1}", moduleId, FormatText(solutionText));
    }


    // Any key entered
    private void KeyPressed(int i) {
        Keys[i].AddInteractionPunch(0.25f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Keys[i].transform);
    }

    // Relevant key entered
    private void RelevantKeyPressed(int keyNo) {
        if (moduleStatus == 1) {
            if (lettersEntered < ScreenText.Length) {
                ScreenText[lettersEntered].text = availableKeys[keyNo];
                lettersEntered++;
                enteredText += availableKeys[keyNo];
            }
        }
    }

    // Delete key pressed
    private void DeleteKeyPressed() {
        if (moduleStatus == 1) {
            if (lettersEntered > 0) {
                ScreenText[lettersEntered - 1].text = "_";
                lettersEntered--;

                if (lettersEntered == 0)
                    enteredText = "";

                else
                    enteredText = enteredText.Substring(0, lettersEntered);
            }
        }
    }


    // View button pressed
    private void ViewButtonPressed() {
        ViewButton.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ViewButton.transform);

        // Switches to the displayed text
        if (moduleStatus == 1) {
            moduleStatus = 0;
            for (int i = 0; i < ScreenText.Length; i++) {
                ScreenText[i].text = givenText.Substring(i, 1);
                ScreenText[i].color = ScreenColors[0];
            }
            InputMode = false;
        }

        // Switches to the terminal
        else if (moduleStatus == 0) {
            moduleStatus = 1;

            for (int i = 0; i < enteredText.Length; i++) {
                ScreenText[i].text = enteredText.Substring(i, 1);
                ScreenText[i].color = ScreenColors[1];
            }

            for (int i = lettersEntered; i < ScreenText.Length; i++) {
                ScreenText[i].text = "_";
                ScreenText[i].color = ScreenColors[1];
            }
            InputMode = true;
        }
    }


    // Submit button pressed
    private void SubmitButtonPressed() {
        SubmitButton.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, SubmitButton.transform);

        if (enteredText == solutionText && moduleStatus == 1) {
            Debug.LogFormat("[Wonder Cipher #{0}] Module solved!", moduleId);
            Module.HandlePass();
            moduleStatus = 2;
            Audio.PlaySoundAtTransform("WonderCipher_Solve", transform);
        }

        else if (moduleStatus == 1) {
            Debug.LogFormat("[Wonder Cipher #{0}] Strike! The wrong message was entered.", moduleId);
            Debug.LogFormat("[Wonder Cipher #{0}] You submitted {1}", moduleId, FormatText(enteredText));
            Module.HandleStrike();
        }
    }


    // Generates a starting message
    private void GenerateStartingMessage() {
        for (int i = 0; i < ScreenText.Length; i++) {
            int rand = UnityEngine.Random.Range(0, availableLetters.Length);
            charValues[i] = rand;
            givenText += availableLetters[rand];
        }
    }

    // Swaps the base 32 numbers in the array
    private void CharSwap() {
        int[] newCharValues = new int[20];

        // Blue Light
        if (redLight == false) {
            newCharValues[0] = charValues[7];
            newCharValues[1] = charValues[4];
            newCharValues[2] = charValues[13];
            newCharValues[3] = charValues[11];
            newCharValues[4] = charValues[0];
            newCharValues[5] = charValues[14];
            newCharValues[6] = charValues[6];
            newCharValues[7] = charValues[1];
            newCharValues[8] = charValues[19];
            newCharValues[9] = charValues[3];
            newCharValues[10] = charValues[9];
            newCharValues[11] = charValues[2];
            newCharValues[12] = charValues[10];
            newCharValues[13] = charValues[12];
            newCharValues[14] = charValues[16];
            newCharValues[15] = charValues[18];
            newCharValues[16] = charValues[15];
            newCharValues[17] = charValues[8];
            newCharValues[18] = charValues[5];
            newCharValues[19] = charValues[17];
        }

        // Red Light
        else {
            newCharValues[0] = charValues[14];
            newCharValues[1] = charValues[4];
            newCharValues[2] = charValues[3];
            newCharValues[3] = charValues[11];
            newCharValues[4] = charValues[9];
            newCharValues[5] = charValues[15];
            newCharValues[6] = charValues[10];
            newCharValues[7] = charValues[2];
            newCharValues[8] = charValues[16];
            newCharValues[9] = charValues[17];
            newCharValues[10] = charValues[7];
            newCharValues[11] = charValues[0];
            newCharValues[12] = charValues[19];
            newCharValues[13] = charValues[13];
            newCharValues[14] = charValues[5];
            newCharValues[15] = charValues[18];
            newCharValues[16] = charValues[6];
            newCharValues[17] = charValues[1];
            newCharValues[18] = charValues[8];
            newCharValues[19] = charValues[12];
        }

        for (int i = 0; i < charValues.Length; i++) {
            charValues[i] = newCharValues[i];
        }

        // Logs the digit values
        string str = "";
        for (int i = 0; i < charValues.Length; i++) {
            str += charValues[i].ToString();
            if (i != charValues.Length - 1)
                str += ", ";
        }

        Debug.LogFormat("[Wonder Cipher #{0}] The decimal values after digit swaps are now: {1}", moduleId, str);
    }


    // Gets the solution message
    private void TranslateToMessage() {
        for (int i = 0; i < charValues.Length; i++) {
            // &67NPR89F0+#STXY45MCHJ-K12=%3Q@W

            string str = "";
            switch (charValues[i]) {
            case 0: str = "&"; break;
            case 1: str = "6"; break;
            case 2: str = "7"; break;
            case 3: str = "N"; break;
            case 4: str = "P"; break;
            case 5: str = "R"; break;
            case 6: str = "8"; break;
            case 7: str = "9"; break;
            case 8: str = "F"; break;
            case 9: str = "0"; break;
            case 10: str = "+"; break;
            case 11: str = "#"; break;
            case 12: str = "S"; break;
            case 13: str = "T"; break;
            case 14: str = "X"; break;
            case 15: str = "Y"; break;
            case 16: str = "4"; break;
            case 17: str = "5"; break;
            case 18: str = "M"; break;
            case 19: str = "C"; break;
            case 20: str = "H"; break;
            case 21: str = "J"; break;
            case 22: str = "-"; break;
            case 23: str = "K"; break;
            case 24: str = "1"; break;
            case 25: str = "2"; break;
            case 26: str = "="; break;
            case 27: str = "%"; break;
            case 28: str = "3"; break;
            case 29: str = "Q"; break;
            case 30: str = "@"; break;
            case 31: str = "W"; break;
            default: str = "&"; break;
            }

            solutionText += str;
        }
    }


    // Formats the on-screen text
    private string FormatText(string code) {
        string str = "";
        int ct = 0;
        foreach (char c in code) {
            str += c;
            ct++;
            if (ct == 5) {
                ct = 0;
                str += " ";
            }
        }
        str = str.Trim();
        return str;
    }


    // Converts the message to a hexadecimal number
    private void ConvertToHex() {
        // Converts the message to binary
        for (int i = 0; i < charValues.Length; i++) {
            switch (charValues[i]) {
            case 0: binaryString += "00000"; break;
            case 1: binaryString += "00001"; break;
            case 2: binaryString += "00010"; break;
            case 3: binaryString += "00011"; break;
            case 4: binaryString += "00100"; break;
            case 5: binaryString += "00101"; break;
            case 6: binaryString += "00110"; break;
            case 7: binaryString += "00111"; break;
            case 8: binaryString += "01000"; break;
            case 9: binaryString += "01001"; break;
            case 10: binaryString += "01010"; break;
            case 11: binaryString += "01011"; break;
            case 12: binaryString += "01100"; break;
            case 13: binaryString += "01101"; break;
            case 14: binaryString += "01110"; break;
            case 15: binaryString += "01111"; break;
            case 16: binaryString += "10000"; break;
            case 17: binaryString += "10001"; break;
            case 18: binaryString += "10010"; break;
            case 19: binaryString += "10011"; break;
            case 20: binaryString += "10100"; break;
            case 21: binaryString += "10101"; break;
            case 22: binaryString += "10110"; break;
            case 23: binaryString += "10111"; break;
            case 24: binaryString += "11000"; break;
            case 25: binaryString += "11001"; break;
            case 26: binaryString += "11010"; break;
            case 27: binaryString += "11011"; break;
            case 28: binaryString += "11100"; break;
            case 29: binaryString += "11101"; break;
            case 30: binaryString += "11110"; break;
            case 31: binaryString += "11111"; break;
            default: binaryString += "00000"; break;
            }
        }

        // Converts the binary number to hexadecimal
        for (int i = 0; i < binaryString.Length; i += 4) {
            switch (binaryString.Substring(i, 4)) {
            case "0000": hexString += "0"; break;
            case "0001": hexString += "1"; break;
            case "0010": hexString += "2"; break;
            case "0011": hexString += "3"; break;
            case "0100": hexString += "4"; break;
            case "0101": hexString += "5"; break;
            case "0110": hexString += "6"; break;
            case "0111": hexString += "7"; break;
            case "1000": hexString += "8"; break;
            case "1001": hexString += "9"; break;
            case "1010": hexString += "A"; break;
            case "1011": hexString += "B"; break;
            case "1100": hexString += "C"; break;
            case "1101": hexString += "D"; break;
            case "1110": hexString += "E"; break;
            case "1111": hexString += "F"; break;
            default: hexString += "0"; break;
            }
        }

        Debug.LogFormat("[Wonder Cipher #{0}] The unmodified hexadecimal number is: {1}", moduleId, hexString);
    }

    // Converts the new hexadecimal number to base 32
    private void ConvertToBase32() {
        // Converts the number from hex to binary
        binaryString = "";

        for (int i = 0; i < hexString.Length; i++) {
            switch (hexString.Substring(i, 1)) {
            case "0": binaryString += "0000"; break;
            case "1": binaryString += "0001"; break;
            case "2": binaryString += "0010"; break;
            case "3": binaryString += "0011"; break;
            case "4": binaryString += "0100"; break;
            case "5": binaryString += "0101"; break;
            case "6": binaryString += "0110"; break;
            case "7": binaryString += "0111"; break;
            case "8": binaryString += "1000"; break;
            case "9": binaryString += "1001"; break;
            case "A": binaryString += "1010"; break;
            case "B": binaryString += "1011"; break;
            case "C": binaryString += "1100"; break;
            case "D": binaryString += "1101"; break;
            case "E": binaryString += "1110"; break;
            case "F": binaryString += "1111"; break;
            default: binaryString += "0000"; break;
            }
        }

        // Converts the binary to base 32
        for (int i = 0; i < charValues.Length; i++) {
            switch (binaryString.Substring(i * 5, 5)) {
            case "00000": charValues[i] = 0; break;
            case "00001": charValues[i] = 1; break;
            case "00010": charValues[i] = 2; break;
            case "00011": charValues[i] = 3; break;
            case "00100": charValues[i] = 4; break;
            case "00101": charValues[i] = 5; break;
            case "00110": charValues[i] = 6; break;
            case "00111": charValues[i] = 7; break;
            case "01000": charValues[i] = 8; break;
            case "01001": charValues[i] = 9; break;
            case "01010": charValues[i] = 10; break;
            case "01011": charValues[i] = 11; break;
            case "01100": charValues[i] = 12; break;
            case "01101": charValues[i] = 13; break;
            case "01110": charValues[i] = 14; break;
            case "01111": charValues[i] = 15; break;
            case "10000": charValues[i] = 16; break;
            case "10001": charValues[i] = 17; break;
            case "10010": charValues[i] = 18; break;
            case "10011": charValues[i] = 19; break;
            case "10100": charValues[i] = 20; break;
            case "10101": charValues[i] = 21; break;
            case "10110": charValues[i] = 22; break;
            case "10111": charValues[i] = 23; break;
            case "11000": charValues[i] = 24; break;
            case "11001": charValues[i] = 25; break;
            case "11010": charValues[i] = 26; break;
            case "11011": charValues[i] = 27; break;
            case "11100": charValues[i] = 28; break;
            case "11101": charValues[i] = 29; break;
            case "11110": charValues[i] = 30; break;
            case "11111": charValues[i] = 31; break;
            default: charValues[1] = 0; break;
            }
        }

        // Logs the digit values
        string str = "";
        for (int i = 0; i < charValues.Length; i++) {
            str += charValues[i].ToString();
            if (i != charValues.Length - 1)
                str += ", ";
        }

        Debug.LogFormat("[Wonder Cipher #{0}] The decimal values of the twenty duotrigesimal are: {1}", moduleId, str);
    }


    // Swaps the hexadecimal pairs
    private void SwapHexPairs() {
        string newHexString = hexString.Substring(0, 1);

        for (int i = 1; i < hexString.Length; i += 2) {
            switch (hexString.Substring(i, 2)) {
            case "00": newHexString += "2E"; break;
            case "01": newHexString += "75"; break;
            case "02": newHexString += "3F"; break;
            case "03": newHexString += "99"; break;
            case "04": newHexString += "09"; break;
            case "05": newHexString += "6C"; break;
            case "06": newHexString += "BC"; break;
            case "07": newHexString += "61"; break;
            case "08": newHexString += "7C"; break;
            case "09": newHexString += "2A"; break;
            case "0A": newHexString += "96"; break;
            case "0B": newHexString += "4A"; break;
            case "0C": newHexString += "F4"; break;
            case "0D": newHexString += "6D"; break;
            case "0E": newHexString += "29"; break;
            case "0F": newHexString += "FA"; break;
            case "10": newHexString += "90"; break;
            case "11": newHexString += "14"; break;
            case "12": newHexString += "9D"; break;
            case "13": newHexString += "33"; break;
            case "14": newHexString += "6F"; break;
            case "15": newHexString += "CB"; break;
            case "16": newHexString += "49"; break;
            case "17": newHexString += "3C"; break;
            case "18": newHexString += "48"; break;
            case "19": newHexString += "80"; break;
            case "1A": newHexString += "7B"; break;
            case "1B": newHexString += "46"; break;
            case "1C": newHexString += "67"; break;
            case "1D": newHexString += "01"; break;
            case "1E": newHexString += "17"; break;
            case "1F": newHexString += "59"; break;
            case "20": newHexString += "B8"; break;
            case "21": newHexString += "FA"; break;
            case "22": newHexString += "70"; break;
            case "23": newHexString += "C0"; break;
            case "24": newHexString += "44"; break;
            case "25": newHexString += "78"; break;
            case "26": newHexString += "48"; break;
            case "27": newHexString += "FB"; break;
            case "28": newHexString += "26"; break;
            case "29": newHexString += "80"; break;
            case "2A": newHexString += "81"; break;
            case "2B": newHexString += "FC"; break;
            case "2C": newHexString += "FD"; break;
            case "2D": newHexString += "61"; break;
            case "2E": newHexString += "70"; break;
            case "2F": newHexString += "C7"; break;
            case "30": newHexString += "FE"; break;
            case "31": newHexString += "A8"; break;
            case "32": newHexString += "70"; break;
            case "33": newHexString += "28"; break;
            case "34": newHexString += "6C"; break;
            case "35": newHexString += "9C"; break;
            case "36": newHexString += "07"; break;
            case "37": newHexString += "A4"; break;
            case "38": newHexString += "CB"; break;
            case "39": newHexString += "3F"; break;
            case "3A": newHexString += "70"; break;
            case "3B": newHexString += "A3"; break;
            case "3C": newHexString += "8C"; break;
            case "3D": newHexString += "D6"; break;
            case "3E": newHexString += "FF"; break;
            case "3F": newHexString += "B0"; break;
            case "40": newHexString += "7A"; break;
            case "41": newHexString += "3A"; break;
            case "42": newHexString += "35"; break;
            case "43": newHexString += "54"; break;
            case "44": newHexString += "E9"; break;
            case "45": newHexString += "9A"; break;
            case "46": newHexString += "3B"; break;
            case "47": newHexString += "61"; break;
            case "48": newHexString += "16"; break;
            case "49": newHexString += "41"; break;
            case "4A": newHexString += "E9"; break;
            case "4B": newHexString += "A3"; break;
            case "4C": newHexString += "90"; break;
            case "4D": newHexString += "A3"; break;
            case "4E": newHexString += "E9"; break;
            case "4F": newHexString += "EE"; break;
            case "50": newHexString += "0E"; break;
            case "51": newHexString += "FA"; break;
            case "52": newHexString += "DC"; break;
            case "53": newHexString += "9B"; break;
            case "54": newHexString += "D6"; break;
            case "55": newHexString += "FB"; break;
            case "56": newHexString += "24"; break;
            case "57": newHexString += "B5"; break;
            case "58": newHexString += "41"; break;
            case "59": newHexString += "9A"; break;
            case "5A": newHexString += "20"; break;
            case "5B": newHexString += "BA"; break;
            case "5C": newHexString += "B3"; break;
            case "5D": newHexString += "51"; break;
            case "5E": newHexString += "7A"; break;
            case "5F": newHexString += "36"; break;
            case "60": newHexString += "3E"; break;
            case "61": newHexString += "60"; break;
            case "62": newHexString += "0E"; break;
            case "63": newHexString += "3D"; break;
            case "64": newHexString += "02"; break;
            case "65": newHexString += "B0"; break;
            case "66": newHexString += "34"; break;
            case "67": newHexString += "57"; break;
            case "68": newHexString += "69"; break;
            case "69": newHexString += "81"; break;
            case "6A": newHexString += "EB"; break;
            case "6B": newHexString += "67"; break;
            case "6C": newHexString += "F3"; break;
            case "6D": newHexString += "EB"; break;
            case "6E": newHexString += "8C"; break;
            case "6F": newHexString += "47"; break;
            case "70": newHexString += "93"; break;
            case "71": newHexString += "CE"; break;
            case "72": newHexString += "2A"; break;
            case "73": newHexString += "AF"; break;
            case "74": newHexString += "35"; break;
            case "75": newHexString += "F4"; break;
            case "76": newHexString += "74"; break;
            case "77": newHexString += "87"; break;
            case "78": newHexString += "50"; break;
            case "79": newHexString += "2C"; break;
            case "7A": newHexString += "39"; break;
            case "7B": newHexString += "68"; break;
            case "7C": newHexString += "BB"; break;
            case "7D": newHexString += "47"; break;
            case "7E": newHexString += "1A"; break;
            case "7F": newHexString += "02"; break;
            case "80": newHexString += "A3"; break;
            case "81": newHexString += "93"; break;
            case "82": newHexString += "64"; break;
            case "83": newHexString += "2E"; break;
            case "84": newHexString += "8C"; break;
            case "85": newHexString += "AD"; break;
            case "86": newHexString += "B1"; break;
            case "87": newHexString += "C4"; break;
            case "88": newHexString += "61"; break;
            case "89": newHexString += "04"; break;
            case "8A": newHexString += "5F"; break;
            case "8B": newHexString += "BD"; break;
            case "8C": newHexString += "59"; break;
            case "8D": newHexString += "21"; break;
            case "8E": newHexString += "1C"; break;
            case "8F": newHexString += "E7"; break;
            case "90": newHexString += "0E"; break;
            case "91": newHexString += "29"; break;
            case "92": newHexString += "26"; break;
            case "93": newHexString += "97"; break;
            case "94": newHexString += "70"; break;
            case "95": newHexString += "A9"; break;
            case "96": newHexString += "CD"; break;
            case "97": newHexString += "18"; break;
            case "98": newHexString += "A3"; break;
            case "99": newHexString += "7B"; break;
            case "9A": newHexString += "74"; break;
            case "9B": newHexString += "70"; break;
            case "9C": newHexString += "96"; break;
            case "9D": newHexString += "DE"; break;
            case "9E": newHexString += "A6"; break;
            case "9F": newHexString += "72"; break;
            case "A0": newHexString += "DD"; break;
            case "A1": newHexString += "13"; break;
            case "A2": newHexString += "93"; break;
            case "A3": newHexString += "AA"; break;
            case "A4": newHexString += "90"; break;
            case "A5": newHexString += "6C"; break;
            case "A6": newHexString += "A7"; break;
            case "A7": newHexString += "B5"; break;
            case "A8": newHexString += "76"; break;
            case "A9": newHexString += "2F"; break;
            case "AA": newHexString += "A8"; break;
            case "AB": newHexString += "7A"; break;
            case "AC": newHexString += "C8"; break;
            case "AD": newHexString += "81"; break;
            case "AE": newHexString += "06"; break;
            case "AF": newHexString += "BB"; break;
            case "B0": newHexString += "85"; break;
            case "B1": newHexString += "75"; break;
            case "B2": newHexString += "11"; break;
            case "B3": newHexString += "0C"; break;
            case "B4": newHexString += "D2"; break;
            case "B5": newHexString += "D1"; break;
            case "B6": newHexString += "C9"; break;
            case "B7": newHexString += "F8"; break;
            case "B8": newHexString += "81"; break;
            case "B9": newHexString += "70"; break;
            case "BA": newHexString += "EE"; break;
            case "BB": newHexString += "C8"; break;
            case "BC": newHexString += "71"; break;
            case "BD": newHexString += "53"; break;
            case "BE": newHexString += "3D"; break;
            case "BF": newHexString += "AF"; break;
            case "C0": newHexString += "76"; break;
            case "C1": newHexString += "CB"; break;
            case "C2": newHexString += "0D"; break;
            case "C3": newHexString += "C1"; break;
            case "C4": newHexString += "56"; break;
            case "C5": newHexString += "28"; break;
            case "C6": newHexString += "E8"; break;
            case "C7": newHexString += "3C"; break;
            case "C8": newHexString += "61"; break;
            case "C9": newHexString += "64"; break;
            case "CA": newHexString += "4B"; break;
            case "CB": newHexString += "B8"; break;
            case "CC": newHexString += "EF"; break;
            case "CD": newHexString += "3B"; break;
            case "CE": newHexString += "41"; break;
            case "CF": newHexString += "09"; break;
            case "D0": newHexString += "72"; break;
            case "D1": newHexString += "07"; break;
            case "D2": newHexString += "50"; break;
            case "D3": newHexString += "AD"; break;
            case "D4": newHexString += "F3"; break;
            case "D5": newHexString += "2E"; break;
            case "D6": newHexString += "5C"; break;
            case "D7": newHexString += "43"; break;
            case "D8": newHexString += "FF"; break;
            case "D9": newHexString += "C3"; break;
            case "DA": newHexString += "B3"; break;
            case "DB": newHexString += "32"; break;
            case "DC": newHexString += "7A"; break;
            case "DD": newHexString += "3E"; break;
            case "DE": newHexString += "9C"; break;
            case "DF": newHexString += "A3"; break;
            case "E0": newHexString += "C2"; break;
            case "E1": newHexString += "AB"; break;
            case "E2": newHexString += "10"; break;
            case "E3": newHexString += "60"; break;
            case "E4": newHexString += "99"; break;
            case "E5": newHexString += "FB"; break;
            case "E6": newHexString += "08"; break;
            case "E7": newHexString += "8A"; break;
            case "E8": newHexString += "90"; break;
            case "E9": newHexString += "57"; break;
            case "EA": newHexString += "8A"; break;
            case "EB": newHexString += "7F"; break;
            case "EC": newHexString += "61"; break;
            case "ED": newHexString += "90"; break;
            case "EE": newHexString += "21"; break;
            case "EF": newHexString += "88"; break;
            case "F0": newHexString += "55"; break;
            case "F1": newHexString += "E8"; break;
            case "F2": newHexString += "FC"; break;
            case "F3": newHexString += "4B"; break;
            case "F4": newHexString += "0D"; break;
            case "F5": newHexString += "4A"; break;
            case "F6": newHexString += "7A"; break;
            case "F7": newHexString += "48"; break;
            case "F8": newHexString += "C9"; break;
            case "F9": newHexString += "B0"; break;
            case "FA": newHexString += "C7"; break;
            case "FB": newHexString += "A6"; break;
            case "FC": newHexString += "D0"; break;
            case "FD": newHexString += "04"; break;
            case "FE": newHexString += "7E"; break;
            case "FF": newHexString += "05"; break;
            default: newHexString += "2E"; break;
            }
        }

        hexString = newHexString;
        Debug.LogFormat("[Wonder Cipher #{0}] The hexadecimal number after swaps is: {1}", moduleId, hexString);
    }


    // Twitch Plays support - made by Fang

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Submit an answer with !{0} submit <input>, reset input with !{0} reset, toggle colorblind mode with !{0} colorblind";
#pragma warning restore 414

    bool InputMode = false;

    IEnumerator ProcessTwitchCommand(string command) {
        if (Regex.IsMatch(command, @"^(?:colorblind)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
            yield return null;
            colorblindMode = !colorblindMode;
            if (colorblindMode == true) {
                if (redLight == true)
                    ColorblindText.text = "RED";

                else
                    ColorblindText.text = "BLUE";
            }
            else
                ColorblindText.text = "";
        }
        else if (Regex.IsMatch(command, @"^(?:reset|clear)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
            yield return null;
            if (enteredText != "") {
                if (!InputMode) {
                    ViewButton.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
                while (enteredText != "") {
                    DeleteKey.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            if (InputMode)
                ViewButton.OnInteract();
        }
        else {
            Match m = Regex.Match(command, @"^(?:submit (.+))$", RegexOptions.IgnoreCase);
            if (m.Success) {
                command = m.Groups[1].ToString().Replace(" ", "");
                if (command.Length != 20) {
                    yield return "sendtochaterror The input length is invalid.";
                    yield break;
                }
                else {
                    char[] inputs = command.ToCharArray();
                    List<KMSelectable> keysToPress = new List<KMSelectable>();

                    foreach (char input in inputs) {
                        if (!keysDict.ContainsKey(input.ToString())) {
                            yield return null;
                            yield return "sendtochaterror The input contains a key that is not pressable on the module.";
                            yield break;
                        }
                        keysToPress.Add(keysDict[input.ToString()]);
                    }
                    keysToPress.Add(SubmitButton);
                    yield return null;
                    if (!InputMode) {
                        ViewButton.OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                    while (enteredText != "") {
                        DeleteKey.OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                    yield return keysToPress.ToArray();
                };
            }
            else {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        }

    }
    IEnumerator TwitchHandleForcedSolve() {
        yield return null;
        if (!InputMode) {
            ViewButton.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (!solutionText.StartsWith(enteredText)) {
            DeleteKey.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        int start = enteredText.Length;
        for (int i = start; i < 20; i++) {
            keysDict[solutionText[i].ToString()].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        SubmitButton.OnInteract();
    }
}