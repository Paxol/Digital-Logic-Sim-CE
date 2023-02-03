using System.Collections.Generic;
using UnityEngine;
using SFB;
using Assets.Scripts.Chip.Interfaces;

public class EEPROM: BuiltinChip, ICustomSaveLogic {
    public static byte[] contents;

    public Pin writePin;

    public int addrBusBytesSize = 2;
    public Pin addrPinPrefab;
    public int dataBusBytesSize = 2;
    public Pin dataInPinPrefab;
    public Pin dataOutPinPrefab;

    public bool autoSaveAndLoad = false;

    private float pinSpacing = 0.15f;
    private float busSpacing = 0.2f;

    private bool pinsInitialized = false;

    protected override void Awake() {
        base.Awake();
        contents = new byte[((long) 1 << (addrBusBytesSize * 8)) * dataBusBytesSize];
        // Debug.Log("EEPROM contents " + contents.Length);
        if (autoSaveAndLoad)
            SaveSystem.LoadEEPROMContents().CopyTo(contents, 0);
    }

    protected override void Start() {
        InitPins();
    }

    private void InitPins() {
        if (!pinsInitialized) {
            // Check if contents array has the right size
            if (contents.Length != ((long)1 << (addrBusBytesSize * 8)) * dataBusBytesSize)
                contents = new byte[((long)1 << (addrBusBytesSize * 8)) * dataBusBytesSize];

            var addrBusBits = addrBusBytesSize * 8;
            var dataBusBits = dataBusBytesSize * 8;

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
        var _addrBusSize = addrBusBytesSize * 8;
        var _dataBusSize = dataBusBytesSize * 8;

        var package = GetComponent < ChipPackage > ();
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

        yoffset -= busSpacing + 16 * pinSpacing;

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
        contents = bytes;
        SaveSystem.SaveEEPROMContents(contents);
    }

    public void DumpBinary() {
        var extensions = new[] {
            new ExtensionFilter("Binary file", "bin"),
        };

        StandaloneFileBrowser.SaveFilePanelAsync(
            "Save binary file", "", "", extensions, (string path) => {
                if (path != null && path != "") {
                    System.IO.File.WriteAllBytes(path, contents);
                }
            });
    }

    protected override void ProcessOutput() {
        int address = 0;
        for (int i = 0; i < (addrBusBytesSize * 8); i++) {
            address <<= 1;
            address += inputPins[i + 1].State;
        }
        int index = address * dataBusBytesSize;
        int data = 0;
        try {
            for (int i = 0; i < dataBusBytesSize; i++) {
                data <<= 8;
                data += contents[index + i];
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
            for (int i = 0; i < (dataBusBytesSize * 8); i++) {
                newData <<= 1;
                newData += inputPins[i + 1 + addrBusBytesSize * 8].State;
            }

            if (newData != data) {
                for (int i = dataBusBytesSize - 1; i >= 0; i--) {
                    try {
                        contents[index + i] = (byte) newData;
                    } catch {
                        Debug.Log("EEPROM write error at index " + (index + i));
                    }
                    newData >>= 8;
                }
                if (autoSaveAndLoad)
                    SaveSystem.SaveEEPROMContents(contents);
            }
        }
    }

    public void HandleLoad(SavedComponentChip savedChip) {
        if (string.IsNullOrEmpty(savedChip.metadata)) return;

        var metadata = JsonUtility.FromJson < EEPROMMetadata > (savedChip.metadata);

        addrBusBytesSize = metadata.addrBusBytesSize;
        dataBusBytesSize = metadata.dataBusBytesSize;

        InitPins();
    }

    public void HandleSaveMetadata(SavedComponentChip savedChip) {
        var json = JsonUtility.ToJson(new EEPROMMetadata(addrBusBytesSize, dataBusBytesSize), false);

        savedChip.metadata = json;
    }
}

[System.Serializable]
class EEPROMMetadata {
    public int addrBusBytesSize;
    public int dataBusBytesSize;

    public EEPROMMetadata(int addrBusBytesSize, int dataBusBytesSize) {
        this.addrBusBytesSize = addrBusBytesSize;
        this.dataBusBytesSize = dataBusBytesSize;
    }
}