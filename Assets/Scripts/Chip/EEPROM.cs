using System.Collections.Generic;
using UnityEngine;
using SFB;
using Assets.Scripts.Chip.Interfaces;

public class EEPROM: BuiltinChip, ICustomSaveLogic {
    public static Dictionary<string, byte[]> loadedMemoryContents = new Dictionary<string, byte[]>();

    public byte[] content
    {
        get => loadedMemoryContents.GetValueOrDefault(id);
        set => loadedMemoryContents[id] = value;
    }

    public Pin writePin;

    public int addrBusBytes = 2;
    public Pin addrPinPrefab;
    public int dataBusBytes = 2;
    public Pin dataInPinPrefab;
    public Pin dataOutPinPrefab;

    public string id = null;
    public bool saveChanges = false;

    private float pinSpacing = 0.15f;
    private float busSpacing = 0.2f;

    private bool pinsInitialized = false;

    protected override void Start()
    {
        base.Start();

        InitPins();
        InitMemoryContent();
    }

    private void InitMemoryContent()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = "EEPROM " + loadedMemoryContents.Keys.Count + 1;

        if (content is null)
            content = (new byte[((long)1 << (addrBusBytes * 8)) * dataBusBytes]);
    }

    private void InitPins() {
        if (!pinsInitialized) {
            var addrBusBits = addrBusBytes * 8;
            var dataBusBits = dataBusBytes * 8;

            var package = GetComponent<ChipPackage>();
            var yoffset = (busSpacing * 4 + (addrBusBits + dataBusBits) * pinSpacing) / 2f;
            if (package != null) {
                package.override_width_and_height = true;
                package.override_height = yoffset * 2f;
                package.SetSizeAndSpacing(this);
            }

            inputPins = new Pin[addrBusBits + dataBusBits + 1];
            inputPins[0] = Instantiate(writePin, transform);

            for (int i = 0; i < addrBusBits; i++) {
                var nextPin = Instantiate(addrPinPrefab, transform);
                nextPin.pinName = "A" + (addrBusBits - i - 1).ToString("X");
                inputPins[i + 1] = nextPin;
            }

            for (int i = 0; i < dataBusBits; i++) {
                var nextPin = Instantiate(dataInPinPrefab, transform);
                nextPin.pinName += (dataBusBits - i - 1).ToString("X");
                inputPins[i + dataBusBits + 1] = nextPin;
            }

            outputPins = new Pin[dataBusBits];
            for (int i = 0; i < dataBusBits; i++) {
                var nextPin = Instantiate(dataOutPinPrefab, transform);
                nextPin.pinName += (dataBusBits - i - 1).ToString("X");
                outputPins[i] = nextPin;
            }

            Destroy(writePin.gameObject);
            Destroy(addrPinPrefab.gameObject);
            Destroy(dataInPinPrefab.gameObject);
            Destroy(dataOutPinPrefab.gameObject);

            pinsInitialized = true;
        }

        UpdatePinPosition();
    }

    private void UpdatePinPosition() {
        var _addrBusSize = addrBusBytes * 8;
        var _dataBusSize = dataBusBytes * 8;

        var package = GetComponent<ChipPackage>();
        var xoffset = 1f;
        var yoffset = (busSpacing * 4 + (_addrBusSize + _dataBusSize) * pinSpacing) / 2f;

        if (package != null)
            xoffset = package.override_width / 2f;

        yoffset -= busSpacing;

        inputPins[0].transform.localPosition = new Vector3(-xoffset, yoffset, 0);

        yoffset -= busSpacing + pinSpacing;

        for (int i = 0; i < _addrBusSize; i++) {
            inputPins[i + 1].transform.localPosition = new Vector3(-xoffset, yoffset - i * pinSpacing, 0);
        }

        yoffset -= busSpacing + 8 * addrBusBytes * pinSpacing;

        for (int i = 0; i < _dataBusSize; i++) {
            inputPins[i + _addrBusSize + 1].transform.localPosition = new Vector3(-xoffset, yoffset - i * pinSpacing, 0);
        }

        for (int i = 0; i < _dataBusSize; i++) {
            outputPins[i].transform.localPosition = new Vector3(xoffset, yoffset - i * pinSpacing, 0);
        }
    }

    private void OnMouseOver() {
        if (Input.GetMouseButtonDown(1))
            UIManager.instance.OpenMenu(MenuType.EEPROMMenu);
    }

    public void OpenAndFlashBinary() {
        Debug.Log("OpenAndFlashBinary");
        var extensions = new[] {
            new ExtensionFilter("Binary file", "bin"),
        };

        StandaloneFileBrowser.OpenFilePanelAsync(
            "Open binary file", "", extensions, true, (string[] paths) => {
                if (paths[0] != null && paths[0] != "") {
                    FlashBinary(paths[0]);
                }
            });
    }

    public void FlashBinary(string path) {
        var bytes = System.IO.File.ReadAllBytes(path);
        content = bytes;
        SaveSystem.SaveEEPROMContent(id, content);
    }

    public void DumpBinary() {
        var extensions = new[] {
            new ExtensionFilter("Binary file", "bin"),
        };

        StandaloneFileBrowser.SaveFilePanelAsync(
            "Save binary file", "", "", extensions, (string path) => {
                if (path != null && path != "") {
                    System.IO.File.WriteAllBytes(path, content);
                }
            });
    }

    protected override void ProcessOutput() {
        int address = 0;
        for (int i = 0; i < (addrBusBytes * 8); i++) {
            address <<= 1;
            address += inputPins[i + 1].State;
        }
        int index = address * dataBusBytes;
        int data = 0;
        try {
            for (int i = 0; i < dataBusBytes; i++) {
                data <<= 8;
                data += content[index + i];
                // Debug.Log("EEPROM read " + contents[index + i] + " at index " + (index + i) + " resulting in data " + data + "");
            }
        } catch {
            Debug.Log("EEPROM read error at address " + (address));
        }

        //reading
        for (int i = 0; i < outputPins.Length; i++) {
            outputPins[i].ReceiveSignal(data & (1 << (outputPins.Length - i - 1)));
        }
        if (inputPins[0].State > 0) {
            //writing
            int newData = 0;
            for (int i = 0; i < (dataBusBytes * 8); i++) {
                newData <<= 1;
                newData += inputPins[i + 1 + addrBusBytes * 8].State;
            }

            if (newData != data) {
                for (int i = dataBusBytes - 1; i >= 0; i--) {
                    try {
                        content[index + i] = (byte)newData;
                    } catch {
                        Debug.Log("EEPROM write error at index " + (index + i));
                    }
                    newData >>= 8;
                }

                if (saveChanges)
                    SaveSystem.SaveEEPROMContent(id, content);
            }
        }
    }

    private void LoadEEPROMContent() {
        content = SaveSystem.LoadEEPROMContent(id);
    }

    public void HandleLoad(SavedComponentChip savedChip) {
        if (string.IsNullOrEmpty(savedChip.metadata)) return;

        var metadata = JsonUtility.FromJson<EEPROMMetadata>(savedChip.metadata);

        addrBusBytes = metadata.addrBusBytesSize;
        dataBusBytes = metadata.dataBusBytesSize;
        id = metadata.id;

        InitPins();
        LoadEEPROMContent();
    }

    public void HandleSaveMetadata(SavedComponentChip savedChip) {
        SaveSystem.SaveEEPROMContent(id, content);
        var json = JsonUtility.ToJson(new EEPROMMetadata(addrBusBytes, dataBusBytes, id.ToString()), false);

        savedChip.metadata = json;
    }
}

[System.Serializable]
class EEPROMMetadata {
    public int addrBusBytesSize;
    public int dataBusBytesSize;
    public string id;

    public EEPROMMetadata(int addrBusBytesSize, int dataBusBytesSize, string id) {
        this.addrBusBytesSize = addrBusBytesSize;
        this.dataBusBytesSize = dataBusBytesSize;
        this.id = id;
    }
}