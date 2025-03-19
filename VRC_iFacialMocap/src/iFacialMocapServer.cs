using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using VRCFaceTracking;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace iFacialMocapTrackingModule
{
    class iFacialMocapServer
    {
        static private int _port = 49983; //port
        private FacialMocapData _trackedData = new();
        private UdpClient? _udpListener, _udpClient;
        public bool isTracking;
        public FacialMocapData FaceData { get { return _trackedData; } }

        /// <summary>
        /// Stops and disposes the clients
        public void Stop()
        {
            if (_udpClient != null) { _udpClient.Close(); _udpClient.Dispose(); }
            if (_udpListener != null) { _udpListener.Close(); _udpListener.Dispose(); }
            FaceData.blends.Clear();
        }
        /// </summary>
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No connection found!");
        }

        /// <summary>
        /// Connects to the Facial Mockup server socket.
        /// </summary>
        /// <param name="ipaddress"></param>
        public void Connect(ref ILogger logger, string ipaddress = "255.255.255.255")
        {
            _udpListener = new(_port);
            _udpClient = new();

            var timeToWait = TimeSpan.FromSeconds(120);

            logger.LogInformation($"Searching iFacialMocap data for {timeToWait.TotalSeconds} seconds on {GetLocalIPAddress()}:{_port}");
            var asyncResult = _udpListener.BeginReceive(null, null);
            asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
            if (asyncResult.IsCompleted)
            {
                try
                {

                    IPEndPoint dstAddr = new(IPAddress.Parse(ipaddress), _port);
                    string data = "iFacialMocap_sahuasouryya9218sauhuiayeta91555dy3719|sendDataVersion=v2";
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    _udpClient.Send(bytes, bytes.Length, dstAddr);
                    _udpListener.Client.ReceiveTimeout = 1000;
                    logger.LogInformation($"Connecting to {GetLocalIPAddress()}:{_port}");
                    isTracking = true;

                }
                catch (Exception e)
                {
                    Stop();
                }

            }

            else
            {
                // Could not connect in time, close module
                logger.LogWarning("Did not receive iFacialMocap data within initialization period, re-initialize the module to try again...");
                isTracking = false;
                return;
            }
        }


        /// <summary>
        /// Reads and parses the data received by the UDP Client, 
        /// storing the facial data result in the Face Data attributes.
        /// </summary>

        public void ReadData(ref ILogger logger)
        {
           if (_udpListener != null)
            {

                  IPEndPoint? RemoteIpEndPoint = null;
                try {
                  byte[] receiveBytes = _udpListener.Receive(ref RemoteIpEndPoint);
                    string returnData = Encoding.ASCII.GetString(receiveBytes);
                    if (isTracking==false)
                    {
                        isTracking = true;
                        logger.LogInformation("Tracking reestablished");
                    }
                    string[] blendData = returnData.Split('|')[1..^1];
                    int i = 0;
                    while (i < blendData.Length) //While in the int attributes
                    {
                        HandleChange(blendData[i], ref logger);
                        i++;
                    }
                }
                catch(Exception e)
                {
                    logger.LogError("Module has disconnected, waiting for reconnection...");
                    isTracking = false;
                }

                }
                else
                {
                    logger.LogError("UDPClient wasn't initialized.");
                }

                /// <summary>
                /// Changes the facial data depending of the assignation received.
                /// </summary>
                /// <param name="blend"></param>
                void HandleChange(string blend, ref ILogger logger)
                {
                if (blend.Contains('#'))
                {
                    string[] assignVal = blend.Split('#');
                    try
                    {
                        string[] unparsedValues = assignVal[1].Split(',');
                        float[] values = new float[unparsedValues.Length];

                        for (int j = 0; j < unparsedValues.Length; j++)
                        {
                            values[j] = float.Parse(unparsedValues[j], CultureInfo.InvariantCulture.NumberFormat);
                        }
                        if (assignVal[0] == "=head")
                        {
                            if (values.Length == 6)
                                _trackedData.head = values;
                            else
                                logger.LogWarning("Insufficient data to assign head's position");
                        }
                        else if (assignVal[0] == "rightEye")
                        {
                            if (values.Length == 3)
                                _trackedData.rightEye = values;
                            else
                                logger.LogWarning("Insufficient data to assign right eye's position");
                        }
                        else if (assignVal[0] == "leftEye")
                        {
                            if (values.Length == 3)
                                _trackedData.leftEye = values;
                            else
                                logger.LogWarning("Insufficient data to assign left eye's position");
                        }
                        else
                        {
                            logger.LogWarning($"Error on setting {assignVal}.");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"Invalid assignation. [{e};;{blend}]");
                        return;
                    }

                }
                else if (blend.Contains('-') || blend.Contains('&'))
                    {
                    char separator = blend.Contains('&') ? '&' : '-';
                        string[] assignVal = blend.Split(separator);
                        try
                        {
                            _trackedData.blends[assignVal[0]] = int.Parse(assignVal[1]);

                        }
                        catch (Exception e)
                        {
                            logger.LogWarning($"Invalid assignation. [{e};;{blend}]");
                            return;
                        }
                    }
                    
                    else
                    {
                        logger.LogWarning($"Data cropped.");
                    }

                }
            
        }
    }
}
