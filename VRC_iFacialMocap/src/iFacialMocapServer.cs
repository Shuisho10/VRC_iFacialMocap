using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;


namespace iFacialMocapTrackingModule
{
    class iFacialMocapServer
    {
        static private int _port = 49983; //port
        private FacialMocapData _trackedData = new();
        private UdpClient? _udpListener, _udpClient;
        private IPEndPoint? _dstAddr;
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
        public static IEnumerable<string> GetLocalIPAddresses()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return ip.ToString();
                }
            }
        }

        /// <summary>
        /// Connects to the Facial Mockup server socket.
        /// </summary>
        /// <param name="ipaddress"></param>
        public bool Connect(ref ILogger logger, string ipaddress = "255.255.255.255")
        {
            isTracking = false;

            _udpListener = new(_port);
            _udpClient = new();
            _dstAddr = new IPEndPoint(IPAddress.Any, _port);

            var timeToWait = TimeSpan.FromSeconds(120);
            // compile list of IP addresses
            string likelyIpAddressList = "";
            string otherIpAddressList = "";
            foreach (string ip in GetLocalIPAddresses())
            {
                if (!string.IsNullOrEmpty(ip))
                {
                    // reference for expected internal IP address format: https://www.okta.com/identity-101/internal-ip/
                    string[] addressBytes = ip.Split('.');
                    string networkPart = $"{addressBytes[0]}.{addressBytes[1]}";

                    // 192.168.0.0 to 192.168.255.255, which offers about 65,000 unique IP addresses 
                    // 10.0.0.0 to 10.255.255.255, a range that provides up to 16 million unique IP addresses
                    if (networkPart.Equals("192.168") || addressBytes[0].Equals("10"))
                    {
                        likelyIpAddressList += $"\n\t{ip}";
                    }
                    else if (addressBytes[0].Equals("172"))
                    {
                        // convert addressBytes[1] to integer
                        int addressByte1 = 0;
                        try
                        {
                            addressByte1 = int.Parse(addressBytes[1]);
                        }
                        catch (FormatException)
                        {
                            logger.LogError("Invalid IP address happened somehow..");
                            continue;
                        }
                        // 172.16.0.0 to 172.31.255.255, providing about 1 million unique IP addresses 
                        if (addressByte1 >= 16 && addressByte1 <= 31)
                        {
                            likelyIpAddressList += $"\n\t{ip}";
                        }
                    }
                    else
                    {
                        otherIpAddressList += $"\n\t{ip}";
                    }
                }
            }
            if (likelyIpAddressList.Length == 0 && otherIpAddressList.Length == 0)
            {
                logger.LogError("No local network connection found!");
                return false;
            }
            logger.LogInformation($"Seeking iFacialMocap connection for {timeToWait.TotalSeconds} seconds. " +
                $"Accepting data on: \n\nIP Address(es)\n========================{likelyIpAddressList}\n========================\n\nUse default iFacialMocap Port: {_port}\n");
            logger.LogDebug($"Other IP Addresses found: \n{otherIpAddressList}\n");

            var asyncResult = _udpListener.BeginReceive(null, null);

            asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
            if (asyncResult.IsCompleted)
            {
                try
                {
                    // EndReceive worked and we have received data and remote endpoint
                    byte[] receivedBytes = _udpListener.EndReceive(asyncResult, ref _dstAddr);
                    logger.LogInformation("Successful message receive");

                    //IPEndPoint dstAddr = new(IPAddress.Parse(ipaddress), _port);
                    string data = "iFacialMocap_sahuasouryya9218sauhuiayeta91555dy3719|sendDataVersion=v2";
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    _udpClient.Send(bytes, bytes.Length, _dstAddr);
                    _udpListener.Client.ReceiveTimeout = 1000;
                    if (_dstAddr == null)
                    {
                        logger.LogWarning("Something went really wrong! Could not identify remote endpoint");
                        return false;
                    }
                    logger.LogInformation($"Connecting to {_dstAddr.Address}:{_dstAddr.Port}");
                    isTracking = true;
                    return isTracking;
                }
                catch (Exception ex)
                {
                    // EndReceive failed and we ended up here
                    logger.LogError($"Error Occurred Attempting Receiving Data: {ex.ToString()}");
                }
            }
            else
            {
                // The operation wasn't completed before the timeout and we're off the hook
                // nothing init so return false
                logger.LogWarning("Did not receive iFacialMocap message within initialization period, re-initialize the module to try again...");
                return false;
            }

            return false;
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
