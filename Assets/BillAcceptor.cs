using System.Collections;
using System.Collections.Generic;
using eSSP_example;
using UnityEngine;
using ITLlib;
using UnityEngine.UI;
using System.IO.Ports;
using System;
using TMPro;
using System.Threading;

public class BillAcceptor : MonoBehaviour
{
    public string portName = "COM3";
    public byte addressSSP = 0;
    // Variables used by this program.
    bool Running = false;
    int pollTimer = 250; // timer in ms
    int reconnectionAttempts = 5;
    CPayout Payout;

    public Button btnRun;
    public Button btnHalt;

    public eSSP_example.TextBox textBox1;

    public TMP_Text logText;

    Thread pollThread;

    public delegate void OnLog(string text);
    public OnLog onLog;

    public TMP_Text serialNumber;

    public string lastLogMessage = "";

    public void Log(string text)
    {
        if (lastLogMessage == text)
        {
            return;
        }
        lastLogMessage = text;
        // prepend it to the log
        logText.text = text + "\n" + logText.text;
    }

    // Start is called before the first frame update
    void Start()
    {
        InitializeCommPort();
        onLog += Log;
    }

    public void StartBillUnit()
    {
        if (Payout == null)
        {
            Payout = new CPayout();
        }
        Payout.CommandStructure.ComPort = portName;
        Payout.CommandStructure.SSPAddress = addressSSP;
        Payout.CommandStructure.Timeout = 3000;

        if (ConnectToValidator(reconnectionAttempts, 2))
        {
            Running = true;
            Log("\r\nPoll Loop\r\n*********************************\r\n");
            Payout.ConfigureBezel(0x00, 0x00, 0xFF, textBox1);
            btnHalt.interactable = true;
            btnRun.interactable = false;

            Log("All done");
            // Start poll coroutine
            Invoke("PollContinuously", pollTimer / 1000);
        }
        else
        {
            Log("Failed to connect to validator");
        }
    }



    public void HaltBillUnit()
    {
        if (Running)
        {
            Payout.DisablePayout(textBox1);
            Payout.DisableValidator(textBox1);
            Running = false;
            btnHalt.interactable = false;
            btnRun.interactable = true;
        }
    }



    void InitializeCommPort()
    {
        // If we're windows, let's try to connect to the bill acceptor
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            InitializeWindowsCommPort();
        }
        // If we're linux, let's try to connect to the bill acceptor
        else if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
        {
            InitializeLinuxCommPort();
        }
        // If we're mac os x, let's try to connect to the bill acceptor
        else if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
        {
            InitializeLinuxCommPort();
        }
    }

    void InitializeLinuxCommPort()
    {
        string[] ports;
        string newport = "";
        try
        {
            ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                if (port.Contains("ttyUSB"))
                {
                    newport = port;
                    break;
                }
                if (port.Contains("usbserial"))
                {
                    newport = port;
                    break;
                }
                if (port.Contains("ttyACM"))
                {
                    newport = port;
                    break;
                }
                if (port.Contains("usbmodem"))
                {
                    newport = port;
                    break;
                }
            }
        }
        catch
        {
            return;
        }
        portName = newport;
        Log("Found port: " + portName);
    }

    void InitializeWindowsCommPort()
    {
        string[] ports;
        try
        {
            ports = SerialPort.GetPortNames();
        }
        catch
        {
            return;
        }

        if (ports.Length <= 0)
        {
            return;
        }
        foreach (string newport in ports)
        {
            // Let's see if the port is between COM2 and COM10, which is what I'll reserve for the bill acceptor
            List<string> availablePorts = new List<string> { "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10", "COM70" };
            if (!availablePorts.Contains(newport))
            {
                continue;
            }

            try
            {
                portName = newport;
                Log("Found port: " + portName);
                break;
            }
            catch
            {
                portName = "";
            }
        }
        Log("Found port: " + portName);
    }

    // Update is called once per frame
    void Update()
    {
        // 
    }

    // This function opens the com port and attempts to connect with the validator. It then negotiates
    // the keys for encryption and performs some other setup commands.
    private bool ConnectToValidator(int attempts, int interval)
    {
        Log("Attempting to connect to validator");
        // run for number of attempts specified
        for (int i = 0; i < attempts; i++)
        {
            // close com port in case it was open
            Payout.SSPComms.CloseComPort();


            // turn encryption off for first stage
            Payout.CommandStructure.EncryptionStatus = false;


            // if the key negotiation is successful then set the rest up
            if (Payout.OpenComPort(textBox1) && Payout.NegotiateKeys(textBox1))
            {
                Payout.CommandStructure.EncryptionStatus = true; // now encrypting
                                                                 // find the max protocol version this validator supports
                byte maxPVersion = FindMaxProtocolVersion();
                if (maxPVersion >= 6)
                {
                    Payout.SetProtocolVersion(maxPVersion, textBox1);
                }
                else
                {
                    Log("This program does not support slaves under protocol 6!");
                    return false;
                }
                // get info from the validator and store useful vars
                Payout.PayoutSetupRequest(textBox1);
                // check this unit is supported
                if (!IsUnitValid(Payout.UnitType))
                {
                    Log("Unsupported type shown by SMART Payout, this SDK supports the SMART Payout only");
                    return false;
                }
                // inhibits, this sets which channels can receive notes
                Payout.SetInhibits(textBox1);
                // Get serial number
                serialNumber.text = Payout.GetSerialNumber();
                // enable, this allows the validator to operate
                Payout.EnableValidator(textBox1);
                // enable the payout system on the validator
                Payout.EnablePayout(textBox1);
                return true;
            }
            // wait for interval before trying again
            System.Threading.Thread.Sleep(interval);
        }
        return false;
    }

    // This function finds the maximum protocol version that a validator supports. To do this
    // it attempts to set a protocol version starting at 6 in this case, and then increments the
    // version until error 0xF8 is returned from the validator which indicates that it has failed
    // to set it. The function then returns the version number one less than the failed version.
    private byte FindMaxProtocolVersion()
    {
        // not dealing with protocol under level 6
        // attempt to set in validator
        byte b = 0x06;
        while (true)
        {
            Payout.SetProtocolVersion(b);
            if (Payout.CommandStructure.ResponseData[0] == CCommands.SSP_RESPONSE_FAIL)
                return --b;
            b++;

            // catch runaway
            if (b > 12)
                return 0x06; // return default
        }
    }

    private bool IsUnitValid(char unitType)
    {
        if (unitType == (char)0x06) // 0x06 is Payout, no other types supported by this program
            return true;
        return false;
    }

    void PollContinuously()
    {
        if (Running)
        {
            Payout.DoPoll();
            Invoke("PollContinuously", pollTimer / 1000);
        }
    }

    public void TryPoll()
    {
        Payout.DoPoll();
    }
}
