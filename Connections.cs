using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;

namespace EDC_Host
{
    class Connections
    {

        //Event-Delegaten
        public delegate void _D_getMessage(string message, string alias);
        public delegate void _D_newConnection(string alias);
        public delegate void _D_blockedConnection(string alias);


        #region Settings
            //Events
            public event _D_getMessage                _getMessage;
            public event _D_newConnection             _newConnection;
            public event _D_blockedConnection         _blockedConnection;


            //Crypt-Class
            private crypto                           _cryptClass;


            //Network Vars
            private System.Net.Sockets.TcpListener   _server;
            private System.Net.Sockets.TcpClient     _client;
            private IPEndPoint                       _ip;
            private int                              _port;
            private bool                             _allowRun         =   false;
            private byte[]                           _heartBeatPackage =   { 5, 255, 70 };
            private bool                             _doHeartBeat      =   true;

            struct singleConnection
            {
                public System.Net.Sockets.TcpClient client;
                public string alias;
            }
            private List<singleConnection>           _allConnections =   new List<singleConnection>();
            private int                              _indexForClientListener = 0;

        #endregion

        //Constructor
        public Connections(EDC_Host.crypto cryptClass, int port)
        {
            _cryptClass = cryptClass;
            _port = port;
        }


        #region Interface

            public void setup()
            {
                _ip = new IPEndPoint(IPAddress.Any, _port);
                _server = new System.Net.Sockets.TcpListener(_ip);
                _server.Start();


                //Neue Clients zulassen
                System.Threading.Thread t = new System.Threading.Thread(listening);
                _allowRun = true;
                t.Start();

                //Eignen Thread für Heartbeats
                t = new System.Threading.Thread(processHeartBeat);
                t.Start();
         
            }

            public void killHost()
            {
                //Sperre neue Verbindungen
                _allowRun = false;


                //Broadcast für Off-Nachricht und Verbindung schließen
                for (int i = 0; i < _allConnections.Count; i++)
                {
                    writeString("HOST GOING OFFLINE", _allConnections[i].client.GetStream());
                    _allConnections[i].client.Close();
                }
                
                //Listener stoppen
                _server.Stop();


            }

            public void disableNewConnections()
            {
                _allowRun = false;
            }

            public void allowNewConnections()
            {
                System.Threading.Thread t = new System.Threading.Thread(listening);
                _allowRun = true;
                t.Start();
            }

            public void sendMessage(string Message)
            {
                writeString(Message, _allConnections[0].client.GetStream());
            }

        #endregion


        #region Interna
            //Auf neue Clienten warten
            private void listening()
         {

              //Nehme neue Verbindungen an, solange neue Verbindungen zugelassen werden.
                while (_allowRun)
                {
                    _client = _server.AcceptTcpClient();

                    //Verhindere neue Verbindung, wenn keine Verbindungen mehr zugelassen werden
                    if (!_allowRun)
                        return;

                    //Lege neue SingleConnection an, alias ist vorläufig die IP
                    singleConnection newClient;
                    newClient.client = _client;
                    newClient.alias = ((IPEndPoint)(_client.Client.RemoteEndPoint)).Address.ToString();


                    //Handshake: 1. die erste Nachricht eines Clienten muss "handshake"(verschlüsselt) sein ; 2. Als Antwort muss "ok"(verschlüsselt) versendet werden
                    //3. Danach schickt der Client seinen Alias.
                    if (readnWaitStream(newClient) == "handshake")
                    {
                        //Handshake Step 2
                        writeString("ok", newClient.client.GetStream());
                        //Handshake Step 3 (get name)
                        newClient.alias = readnWaitStream(newClient);
                        raiseNewConnection(newClient.alias);

                        //Neue Verbindung in Liste der Verbindungen aufnehmen
                        _allConnections.Add(newClient);
                        _indexForClientListener = _allConnections.Count - 1;

                        //Eigenen Listener für den neuen Clienten
                        System.Threading.Thread t = new System.Threading.Thread(clientListener);
                        t.Start();

                    }
                    else
                    {
                        raiseBlockedConnection(newClient.alias);
                        newClient.client.Close();

                    }
                }

        }


            private void clientListener()
            {
                //Index der Verbindung erfassen - fragwürdiger Weg
                int index = _indexForClientListener;
                //Stopuhr für Hearbeat
                System.Diagnostics.Stopwatch hbTimer = new System.Diagnostics.Stopwatch();


                hbTimer.Start();
                while (_allConnections[index].client.Connected)
                {
                    if (_allConnections[index].client.GetStream().DataAvailable)
                    {
                        string buffer = readStream(_allConnections[index].client.GetStream());

                        if (buffer != "_heartbeat_")
                            raiseGetMessage(buffer, _allConnections[index].alias);

                        hbTimer.Restart();
                    }


                    if (hbTimer.ElapsedMilliseconds > 30000)
                        break;
                }

                _allConnections[index].client.Close();
                Console.WriteLine("{0} disconnected", _allConnections[index].alias);

            }


            private int pingAtGoogle()
            {
                Ping pingSender = new Ping();
                PingOptions opt = new PingOptions();

                opt.DontFragment = true;

                string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                byte[] buffer = System.Text.Encoding.ASCII.GetBytes(data);
                int timeout = 120;
                PingReply reply = pingSender.Send("8.8.8.8", timeout, buffer, opt);

                if (reply.Status == IPStatus.Success)
                    return Convert.ToInt32(reply.RoundtripTime);
                else
                    return -1;
            }

            //Help-Functions
            private string getHexString(byte[] text)
            {
                String hexCode = "";
                for (int i = 0; i < text.Length; i++)
                    hexCode += String.Format("{0:X}", Convert.ToInt32(text[i]));
                return hexCode;
            }
            private string getHexString(byte text)
            {
                String hexCode = "";
                hexCode += String.Format("{0:X}", Convert.ToInt32(text));
                return hexCode;
            }

            //HeartBeat
            private void sendHeartBeat(System.Net.Sockets.NetworkStream stream)
            {
                //HearbeatPackage in den Netzwerkstream schreiben und abschicken
                stream.Write(_heartBeatPackage, 0, _heartBeatPackage.Length);
                stream.Flush();

            }
            private void processHeartBeat()
            {
                //Timer für HeartBeat
                System.Diagnostics.Stopwatch hbTimer = new System.Diagnostics.Stopwatch();
                hbTimer.Start();


                while (_doHeartBeat)
                {
                    if (hbTimer.ElapsedMilliseconds >= 5000)
                    {
                        foreach (singleConnection con in _allConnections)
                            sendHeartBeat(con.client.GetStream());

                        hbTimer.Restart();
                    }
                }
            }


            //Read'n'Write-Functions
            private string readStream(System.Net.Sockets.NetworkStream stream)
            {
                List<byte> buffer = new List<byte>();

                while (stream.DataAvailable)
                {
                    buffer.Add(Convert.ToByte(stream.ReadByte()));
                }

                if (buffer.Count == 3)
                    if (_heartBeatPackage.SequenceEqual(buffer.ToArray()))
                        return "_heartbeat_";

                string output = _cryptClass.decryptMessage(buffer.ToArray());

                return output;
            }
            private string readnWaitStream(singleConnection con)
            {
                List<byte> buffer = new List<byte>();

                //Warte bis Daten verfügbar sind
                do { } while (!con.client.GetStream().DataAvailable);

                //Lese Daten aus NetworkStream, bis keine mehr Verfügbar sind
                while (con.client.GetStream().DataAvailable)
                    buffer.Add(Convert.ToByte(con.client.GetStream().ReadByte()));

                //Gebe entschlüsselten Nachrichtentext zurück
                return _cryptClass.decryptMessage(buffer.ToArray());
            }
            private void writeString(string text, System.Net.Sockets.NetworkStream stream)
            {
                byte[] buffer = _cryptClass.encyrptMessage(text);
                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
            }

        #endregion

        #region Eventhandler

            private void raiseGetMessage(string message, string alias)
            {
                if (_getMessage != null)
                    _getMessage(message, alias);
            }

            private void raiseNewConnection(string alias)
            {
                if (_newConnection != null)
                    _newConnection(alias);
            }

            private void raiseBlockedConnection(string alias)
            {
                if (_blockedConnection != null)
                    _blockedConnection(alias);
            }

        #endregion

    }
}
