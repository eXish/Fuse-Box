using System.Collections;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using UnityEngine.Windows.Speech;

public class FuseBoxScript : MonoBehaviour {

    public KMAudio audio;
    public KMBombInfo bomb;
    public KMSelectable[] buttons;
    public Transform lever;
    public Transform door;
    public MeshRenderer light;
    public Material[] lightMats;
    public TextMesh arrowDisplay;
    public Transform[] switches;
    public Transform[] wireGroups;

    KeywordRecognizer mic;
    string[] bannedWords = { "red", "green", "blue", "yellow", "left", "right", "top", "bottom", "up", "down", "switch", "flash", "simon", "sequence", "wire", "binary", "one", "zero", "arrow", "display", "peanut butter" };
    bool powerOn;
    bool opened;
    bool animating;
    string[] colorNames = { "Red", "Yellow", "Green", "Blue" };
    int[] lightColors = new int[4];
    string[] switchCombos = new string[8];
    string arrows = "↖↗↙↘";
    int[] switchPos = new int[3];
    int[] correctButtons = new int[4];
    int[] inputButtons = { -1, -1, -1, -1 };
    int presses;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressButton(pressed); return false; };
        }
    }

    void Start()
    {
        mic = new KeywordRecognizer(bannedWords, ConfidenceLevel.Low);
        mic.OnPhraseRecognized += OnBannedWord;
        for (int i = 0; i < 4; i++)
            lightColors[i] = UnityEngine.Random.Range(0, 4);
        Debug.LogFormat("[The Fuse Box #{0}] Flashing colors: {1} {2} {3} {4}", moduleId, colorNames[lightColors[0]], colorNames[lightColors[1]], colorNames[lightColors[2]], colorNames[lightColors[3]]);
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 4; j++)
                switchCombos[i] += arrows[UnityEngine.Random.Range(0, 4)];
        }
        int[] wireChoices = { 0, 1, 2, 3 };
        int[] wireHeightChoices = { 0, 1, 2, 3 };
        wireChoices = wireChoices.Shuffle();
        wireHeightChoices = wireHeightChoices.Shuffle();
        for (int i = 0; i < 4; i++)
        {
            wireGroups[i].GetChild(wireChoices[i]).gameObject.SetActive(true);
            switch (wireHeightChoices[i])
            {
                case 1:
                    wireGroups[i].GetChild(wireChoices[i]).localScale += new Vector3(0, 0.15f, 0);
                    wireGroups[i].GetChild(wireChoices[i]).localPosition += new Vector3(0, -0.05f, 0);
                    break;
                case 2:
                    wireGroups[i].GetChild(wireChoices[i]).localScale += new Vector3(0, 0.3f, 0);
                    wireGroups[i].GetChild(wireChoices[i]).localPosition += new Vector3(0, -0.05f, 0);
                    break;
                case 3:
                    wireGroups[i].GetChild(wireChoices[i]).localScale += new Vector3(0, 0.5f, 0);
                    wireGroups[i].GetChild(wireChoices[i]).localPosition += new Vector3(0, -0.05f, 0);
                    break;
                default:
                    break;
            }
        }
        for (int i = 0; i < 4; i++)
            Debug.LogFormat("[The Fuse Box #{0}] Wire {1} is connected to top port {2}", moduleId, i + 1, wireChoices[0] + 1);
        bool[] leftBit = new bool[3];
        bool[] rightBit = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            if (wireHeightChoices[i] > wireHeightChoices[i + 1])
                rightBit[i] = true;
        }
        if (wireChoices[0] != 0)
            leftBit[0] = true;
        if (wireChoices[1] != 1 || (wireChoices[1] == 1 && wireChoices[0] != 0))
            leftBit[1] = true;
        if (wireChoices[2] != 2 || (wireChoices[2] == 2 && wireChoices[3] != 3))
            leftBit[2] = true;
        Debug.LogFormat("[The Fuse Box #{0}] Binary strings: {1}{2} {3}{4} {5}{6}", moduleId, leftBit[0] ? 1 : 0, rightBit[0] ? 1 : 0, leftBit[1] ? 1 : 0, rightBit[1] ? 1 : 0, leftBit[2] ? 1 : 0, rightBit[2] ? 1 : 0);
        int[] bitResults = new int[3];
        for (int i = 0; i < 3; i++)
        {
            int higherWire = wireHeightChoices[i] > wireHeightChoices[i + 1] ? i : i + 1;
            bool result = Logic(lightColors[higherWire], leftBit[i], rightBit[i]);
            bitResults[i] = result ? 1 : -1;
        }
        Debug.LogFormat("[The Fuse Box #{0}] Bits after applying logic: {1} {2} {3}", moduleId, bitResults[0] == -1 ? 0 : 1, bitResults[1] == -1 ? 0 : 1, bitResults[2] == -1 ? 0 : 1);
        string goalArrows = SwitchCombination(bitResults[0], bitResults[1], bitResults[2]);
        Debug.LogFormat("[The Fuse Box #{0}] Arrow sequence from correct switch states: {1}", moduleId, goalArrows);
        for (int i = 0; i < 4; i++)
            correctButtons[i] = arrows.IndexOf(goalArrows[i]);
        Debug.LogFormat("[The Fuse Box #{0}] Expected button sequence: {1} {2} {3} {4}", moduleId, correctButtons[0] + 1, correctButtons[1] + 1, correctButtons[2] + 1, correctButtons[3] + 1);
    }

    void PressButton(KMSelectable pressed)
    {
        if (!animating)
        {
            int index = Array.IndexOf(buttons, pressed);
            if (index == 8)
            {
                audio.PlaySoundAtTransform("metalDoor", transform);
                StartCoroutine(ToggleDoor());
            }
            else if (index == 0 && !powerOn && opened)
            {
                audio.PlaySoundAtTransform("leverPull", pressed.transform);
                StartCoroutine(LeverPull());
            }
            else if (powerOn && opened && !moduleSolved)
            {
                if (index > 0 && index < 4)
                {
                    audio.PlaySoundAtTransform("switchClick", pressed.transform);
                    if (switchPos[index - 1] == 1)
                    {
                        switchPos[index - 1] = -1;
                        switches[index - 1].localPosition += new Vector3(0, 0, -.94f);
                    }
                    else if (switchPos[index - 1] == -1)
                    {
                        switchPos[index - 1] = 1;
                        switches[index - 1].localPosition += new Vector3(0, 0, .94f);
                    }
                    else
                    {
                        switchPos[index - 1] = 1;
                        switches[index - 1].localPosition += new Vector3(0, 0, .47f);
                    }
                    arrowDisplay.text = SwitchCombination(switchPos[0], switchPos[1], switchPos[2]);
                }
                else if (index > 3 && index < 8)
                {
                    pressed.AddInteractionPunch(.5f);
                    audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
                    inputButtons[presses] = index - 4;
                    presses++;
                    if (presses == 4)
                    {
                        Debug.LogFormat("[The Fuse Box #{0}] Inputted button sequence: {1} {2} {3} {4}", moduleId, inputButtons[0] + 1, inputButtons[1] + 1, inputButtons[2] + 1, inputButtons[3] + 1);
                        if (inputButtons.SequenceEqual(correctButtons))
                        {
                            Debug.LogFormat("[The Fuse Box #{0}] Correct, module solved", moduleId);
                            moduleSolved = true;
                            GetComponent<KMBombModule>().HandlePass();
                            mic.Stop();
                            if (TwitchPlaysActive)
                                buttons[8].OnInteract();
                        }
                        else
                        {
                            Debug.LogFormat("[The Fuse Box #{0}] Incorrect, strike and inputs cleared", moduleId);
                            GetComponent<KMBombModule>().HandleStrike();
                            inputButtons = new int[] { -1, -1, -1, -1 };
                            presses = 0;
                        }
                    }
                }
            }
        }
    }

    string SwitchCombination(int pos1, int pos2, int pos3)
    {
        if (pos1 == -1 && pos2 == -1 && pos3 == -1)
            return switchCombos[0];
        else if (pos1 == -1 && pos2 == -1 && pos3 == 1)
            return switchCombos[1];
        else if (pos1 == -1 && pos2 == 1 && pos3 == -1)
            return switchCombos[2];
        else if (pos1 == 1 && pos2 == -1 && pos3 == -1)
            return switchCombos[3];
        else if (pos1 == -1 && pos2 == 1 && pos3 == 1)
            return switchCombos[4];
        else if (pos1 == 1 && pos2 == 1 && pos3 == -1)
            return switchCombos[5];
        else if (pos1 == 1 && pos2 == -1 && pos3 == 1)
            return switchCombos[6];
        else if (pos1 == 1 && pos2 == 1 && pos3 == 1)
            return switchCombos[7];
        return "";
    }

    bool Logic(int gate, bool input1, bool input2)
    {
        switch (gate)
        {
            case 0:
                return input1 && input2;
            case 1:
                return input1 || input2;
            case 2:
                return input1 != input2;
            default:
                return input1 == input2;
        }
    }

    void OnBannedWord(PhraseRecognizedEventArgs args)
    {
        Debug.LogFormat("[The Fuse Box #{0}] I heard you say \"{1}\", I don't like that", moduleId, args.text);
        GetComponent<KMBombModule>().HandleStrike();
    }

    IEnumerator ToggleDoor()
    {
        animating = true;
        Vector3 initPos = door.localPosition;
        Vector3 finalPos = door.localPosition;
        finalPos.x *= -1;
        float t = 0f;
        while (t < 1f)
        {
            yield return null;
            t += Time.deltaTime * 1.3f;
            door.localPosition = Vector3.Lerp(initPos, finalPos, t);
        }
        animating = false;
        opened = !opened;
    }

    IEnumerator LeverPull()
    {
        animating = true;
        Vector3 initRot = lever.localEulerAngles;
        Vector3 finalRot = lever.localEulerAngles;
        finalRot.z = -68;
        float t = 0f;
        while (t < 1f)
        {
            yield return null;
            t += Time.deltaTime * 5f;
            lever.localEulerAngles = Vector3.Lerp(initRot, finalRot, t);
        }
        StartCoroutine(LightCycle());
        mic.Start();
        animating = false;
        powerOn = true;
    }

    IEnumerator LightCycle()
    {
        while (true)
        {
            light.material = lightMats[lightColors[0]];
            yield return new WaitForSeconds(1f);
            light.material = lightMats[lightColors[1]];
            yield return new WaitForSeconds(1f);
            light.material = lightMats[lightColors[2]];
            yield return new WaitForSeconds(1f);
            light.material = lightMats[lightColors[3]];
            yield return new WaitForSeconds(1f);
            light.material = lightMats[4];
            yield return new WaitForSeconds(2f);
        }
    }

    //twitch plays
    bool TwitchPlaysActive;
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} cover [Moves the cover] | !{0} lever [Flips the power lever] | !{0} switches udu [Sets the switches to up or down] | !{0} press 1234 [Presses the keys in reading order]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*cover\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            buttons[8].OnInteract();
            yield break;
        }
        if (Regex.IsMatch(command, @"^\s*lever\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            buttons[0].OnInteract();
            yield break;
        }
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*switches\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length == 1)
            {
                yield return "sendtochaterror Please specify the states you wish to set the switches to!";
                yield break;
            }
            if (parameters.Length > 2)
            {
                yield return "sendtochaterror Too many parameters!";
                yield break;
            }
            if (parameters[1].Length != 3)
            {
                yield return "sendtochaterror Please include exactly 3 switch states!";
                yield break;
            }
            if (!parameters[1][0].EqualsAny('u', 'd', 'U', 'D') && !parameters[1][1].EqualsAny('u', 'd', 'U', 'D') && !parameters[1][2].EqualsAny('u', 'd', 'U', 'D'))
            {
                yield return "sendtochaterror The specified set of switch states is invalid!";
                yield break;
            }
            for (int i = 0; i < 3; i++)
            {
                if (parameters[1][i].EqualsAny('u', 'U') && switchPos[i] != 1)
                {
                    buttons[i + 1].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                else if (parameters[1][i].EqualsAny('d', 'D') && switchPos[i] != -1)
                {
                    while (switchPos[i] != -1)
                    {
                        buttons[i + 1].OnInteract();
                        yield return new WaitForSeconds(.1f);
                    }
                }
            }
            yield break;
        }
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length == 1)
            {
                yield return "sendtochaterror Please specify the keys you wish to press!";
                yield break;
            }
            if (parameters.Length > 2)
            {
                yield return "sendtochaterror Too many parameters!";
                yield break;
            }
            for (int i = 0; i < parameters[1].Length; i++)
            {
                if (!parameters[1][i].EqualsAny('1', '2', '3', '4'))
                {
                    yield return "sendtochaterror The specified set of keys to press is invalid!";
                    yield break;
                }
            }
            for (int i = 0; i < parameters[1].Length; i++)
            {
                buttons[int.Parse(parameters[1][i].ToString()) + 3].OnInteract();
                yield return new WaitForSeconds(.1f);
            }
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if (presses != 0)
        {
            for (int i = 0; i < presses; i++)
            {
                if (inputButtons[i] != correctButtons[i])
                {
                    moduleSolved = true;
                    GetComponent<KMBombModule>().HandlePass();
                    mic.Stop();
                    if (opened)
                        buttons[8].OnInteract();
                    yield break;
                }
            }
        }
        while (animating) yield return true;
        if (!opened)
        {
            buttons[8].OnInteract();
            while (animating) yield return true;
        }
        if (!powerOn)
        {
            buttons[0].OnInteract();
            while (animating) yield return true;
        }
        int start = presses;
        for (int i = start; i < 4; i++)
        {
            buttons[correctButtons[i] + 4].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
    }
}