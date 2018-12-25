using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using MathNet.Numerics.LinearAlgebra;
using System.Numerics;
using System.Threading;
using Newtonsoft.Json;

namespace test
{

    public class KeyGenerator
    {
        public struct User
        {
            public string name;
            public Matrix<double> publicKey;
            public Matrix<double> secretKey;
        }

        public List<User> usersList = new List<User>();
        public int keySize;
        public int module;
        public Matrix<double> initialMatrix;

        public KeyGenerator(int ksize, int mod)
        {
            this.module = mod;
            this.keySize = ksize;

            Random matrixRandomizer = new Random();

            this.initialMatrix = new MathNet.Numerics.LinearAlgebra.Double.DenseMatrix(keySize, keySize);

            for (int i = 0; i < keySize; i++)
            {
                for(int j = i; j < keySize; j++)
                {
                    this.initialMatrix[i, j] = matrixRandomizer.Next(1, 100);
                    if(i != j)
                    {
                        this.initialMatrix[j, i] = this.initialMatrix[i, j]; 
                    }
                }
            }

            this.initialMatrix = this.initialMatrix.Modulus(module);
        }

        public void addUser(string name)
        {
            User newUser = new User { name = name };
            double[] pKey = new double[keySize];
            double[] sKey = new double[keySize];

            Random keyRandomizer = new Random();

            for (int i = 0; i < keySize; i++)
            {
                pKey[i] = keyRandomizer.Next(0, module);
            }

            newUser.publicKey = new MathNet.Numerics.LinearAlgebra.Double.DenseMatrix(keySize, 1, pKey);
            newUser.secretKey = initialMatrix.Multiply(newUser.publicKey);
            newUser.secretKey = newUser.secretKey.Modulus(module);

            usersList.Add(newUser);

            Console.WriteLine("PUBLIC KEY: " + newUser.publicKey.ToString());
            Console.WriteLine("SECRET KEY: " + newUser.secretKey.ToString());
        }

        public User? getUser(string name)
        {
            for(int i = 0; i < usersList.Count; i++)
            {
                if(usersList[i].name == name)
                {
                    return usersList[i];
                }
            }
            return null;
        }

        public void calcKey()
        {
            User ui = (User)getUser("ilya");
            User uk = (User)getUser("kostyan");
            Matrix<double> zzz1 = ui.secretKey.Transpose();
            zzz1 = zzz1.Multiply(uk.publicKey);
            zzz1 = zzz1.Modulus(module);
            Matrix<double> zzz2 = uk.secretKey.Transpose();
            zzz2 = zzz2.Multiply(ui.publicKey);
            zzz2 = zzz2.Modulus(module);

            Console.WriteLine("SECRET 1: " + zzz1.ToString());
            Console.WriteLine("SECRET 2: " + zzz2.ToString());
        }

        public string getKeySet(string name, string companion)
        {
            User u = (User)getUser(name);
            User c = (User)getUser(companion);
            return String.Join(" ", u.publicKey.EnumerateRows().SelectMany(x => x.Enumerate())) + "|" + String.Join(" ", u.secretKey.EnumerateRows().SelectMany(x => x.Enumerate())) + "|"+ String.Join(" ", c.publicKey.EnumerateRows().SelectMany(x => x.Enumerate()));
        }
    }

    public struct serverMessage
    {
        public string type;
        public string data;
        public string name;
    }

    public class ClientObject
    {
        protected internal string Id { get; private set; }
        protected internal NetworkStream Stream { get; private set; }
        public string userName;
        TcpClient client;
        ServerObject server;

        public struct connections
        {
            public string userName;
            public KeyGenerator kg;
        }

        List<connections> userConnections = new List<connections>();

        public string getConnections()
        {
            string res = "";
            for(int i = 0; i < userConnections.Count; i++)
            {
                res += userConnections[i].userName;
                if(i != userConnections.Count - 1)
                {
                    res += "|";
                }
                if(userConnections.Count == 0)
                {
                    res = "";
                }
            }
            return res;
        }

        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            Id = Guid.NewGuid().ToString();
            client = tcpClient;
            server = serverObject;
            serverObject.AddConnection(this);
        }

        public void Process()
        {
            try
            {
                Stream = client.GetStream();
                string message = GetMessage();
                userName = message;

                message = "Пользователь " + userName + " подключился к серверу";
                //server.BroadcastMessage(message, this.Id);
                Console.WriteLine(message);
                while (true)
                {
                    try
                    {
                        message = GetMessage();
                        serverMessage _sm = JsonConvert.DeserializeObject<serverMessage>(message);
                        string json;
                        byte[] data;
                        bool userExists = false;
                        bool isAlreadyConnected = false;
                        switch (_sm.type)
                        {
                            case "connections":
                                serverMessage connections = new serverMessage() { type = "connections", data = getConnections() };
                                json = JsonConvert.SerializeObject(connections);
                                data = Encoding.Unicode.GetBytes(json);
                                Stream.Write(data, 0, data.Length);
                                break;
                            case "disconnect":
                                isAlreadyConnected = false;
                                for (int i = 0; i < userConnections.Count; i++)
                                {
                                    if (userConnections[i].userName == _sm.data)
                                    {
                                        isAlreadyConnected = true;
                                        userConnections.RemoveAt(i);
                                        serverMessage removeconnection = new serverMessage() { type = "removeconnection", data = _sm.data };
                                        json = JsonConvert.SerializeObject(removeconnection);
                                        data = Encoding.Unicode.GetBytes(json);
                                        Stream.Write(data, 0, data.Length);
                                        for (int j = 0; j < server.clients.Count; j++)
                                        {
                                            if (server.clients[j].userName == _sm.data)
                                            {
                                                for (int k = 0; k < server.clients[j].userConnections.Count; k++)
                                                {
                                                    if(server.clients[j].userConnections[k].userName == userName)
                                                    {
                                                        server.clients[j].userConnections.RemoveAt(k);
                                                        serverMessage connectionremoved = new serverMessage() { type = "connectionremoved", data = userName };
                                                        json = JsonConvert.SerializeObject(connectionremoved);
                                                        data = Encoding.Unicode.GetBytes(json);
                                                        server.clients[j].Stream.Write(data, 0, data.Length);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                if(isAlreadyConnected == false)
                                {
                                    serverMessage alreadydisconnected = new serverMessage() { type = "alreadydisconnected", data = _sm.data };
                                    json = JsonConvert.SerializeObject(alreadydisconnected);
                                    data = Encoding.Unicode.GetBytes(json);
                                    Stream.Write(data, 0, data.Length);
                                }
                                break;
                            case "usersonline":
                                serverMessage onlineUsersResponse = new serverMessage() { type = "usersonline", data = String.Join("|", server.getOnlineUsers(), 0, server.clients.Count) };
                                json = JsonConvert.SerializeObject(onlineUsersResponse);
                                data = Encoding.Unicode.GetBytes(json);
                                Stream.Write(data, 0, data.Length);
                                break;
                            case "connect":
                                isAlreadyConnected = false;
                                for(int i = 0; i < userConnections.Count; i++)
                                {
                                    if(userConnections[i].userName == _sm.data)
                                    {
                                        serverMessage acceptConnection = new serverMessage() { type = "rejectconnection", data = "пользователь с таким именем уже связан с вами" };
                                        json = JsonConvert.SerializeObject(acceptConnection);
                                        data = Encoding.Unicode.GetBytes(json);
                                        Stream.Write(data, 0, data.Length);
                                        isAlreadyConnected = true;
                                        break;
                                    }
                                }
                                if(isAlreadyConnected == true)
                                {
                                    break;
                                }
                                userExists = false;
                                for (int i = 0; i < server.clients.Count; i++)
                                {
                                    if(server.clients[i].userName == _sm.data)
                                    {
                                        userExists = true;
                                        serverMessage acceptConnection = new serverMessage() { type = "acceptconnection", data = userName };
                                        json = JsonConvert.SerializeObject(acceptConnection);
                                        data = Encoding.Unicode.GetBytes(json);
                                        server.clients[i].Stream.Write(data, 0, data.Length);
                                        break;
                                    }
                                }
                                if (userExists == false)
                                {
                                    serverMessage acceptConnection = new serverMessage() { type = "rejectconnection", data = "пользователя с таким именем не существует" };
                                    json = JsonConvert.SerializeObject(acceptConnection);
                                    data = Encoding.Unicode.GetBytes(json);
                                    Stream.Write(data, 0, data.Length);
                                    break;
                                }
                                break;
                            case "rejectconnection":
                                for (int i = 0; i < server.clients.Count; i++)
                                {
                                    if (server.clients[i].userName == _sm.data)
                                    {
                                        serverMessage acceptConnection = new serverMessage() { type = "rejectconnection", data = userName };
                                        json = JsonConvert.SerializeObject(acceptConnection);
                                        data = Encoding.Unicode.GetBytes(json);
                                        server.clients[i].Stream.Write(data, 0, data.Length);
                                        break;
                                    }
                                }
                                break;
                            case "accept":
                                KeyGenerator newKeyGen = new KeyGenerator(5, 50);
                                newKeyGen.addUser(userName);
                                newKeyGen.addUser(_sm.data);
                                string res1 = newKeyGen.getKeySet(userName, _sm.data);
                                string res2 = newKeyGen.getKeySet(_sm.data, userName);
                                userConnections.Add(new connections() { userName = _sm.data, kg = newKeyGen});
                                for (int i = 0; i < server.clients.Count; i++)
                                {
                                    if(server.clients[i].userName == _sm.data)
                                    {
                                        server.clients[i].userConnections.Add(new connections() { userName = userName, kg = newKeyGen});
                                        
                                        serverMessage r2 = new serverMessage() { type = "accept", data = res2, name = userName };
                                        json = JsonConvert.SerializeObject(r2);
                                        data = Encoding.Unicode.GetBytes(json);
                                        server.clients[i].Stream.Write(data, 0, data.Length);
                                        serverMessage r1 = new serverMessage() { type = "accept", data = res1, name = server.clients[i].userName };
                                        json = JsonConvert.SerializeObject(r1);
                                        data = Encoding.Unicode.GetBytes(json);
                                        Stream.Write(data, 0, data.Length);
                                        break;
                                    }
                                }

                                break;
                            default:
                                string fp = _sm.type.Split('|')[0];
                                string sp = _sm.type.Split('|')[1];
                                bool isConnectionExists = false;
                                for (int i = 0; i < userConnections.Count; i++)
                                {
                                    if(userConnections[i].userName == sp && fp == "message")
                                    {
                                        for (int j = 0; j < server.clients.Count; j++)
                                        {
                                            if(server.clients[j].userName == sp)
                                            {
                                                serverMessage mess = new serverMessage() { type = fp+'|'+userName, data = _sm.data };
                                                json = JsonConvert.SerializeObject(mess);
                                                data = Encoding.Unicode.GetBytes(json);
                                                server.clients[j].Stream.Write(data, 0, data.Length);
                                                isConnectionExists = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (isConnectionExists == false)
                                {
                                    serverMessage messerr = new serverMessage() { type = "messageerror", data = _sm.data };
                                    json = JsonConvert.SerializeObject(messerr);
                                    data = Encoding.Unicode.GetBytes(json);
                                    Stream.Write(data, 0, data.Length);
                                    break;
                                }
                                break;
                        }
                        message = String.Format("{0}: {1}", userName, message);
                        Console.WriteLine(message);
                        //server.BroadcastMessage(message, this.Id);
                    }
                    catch
                    {
                        message = String.Format("{0}: отключился от сервера", userName);
                        Console.WriteLine(message);
                        //server.BroadcastMessage(message, this.Id);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                server.RemoveConnection(this.Id);
                Close();
            }
        }
        
        private string GetMessage()
        {
            byte[] data = new byte[64];
            StringBuilder builder = new StringBuilder();
            int bytes = 0;
            do
            {
                bytes = Stream.Read(data, 0, data.Length);
                builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
            }
            while (Stream.DataAvailable);

            return builder.ToString();
        }
        
        protected internal void Close()
        {
            if (Stream != null)
                Stream.Close();
            if (client != null)
                client.Close();
        }
    }

    public class ServerObject
    {
        static TcpListener tcpListener; 
        public List<ClientObject> clients = new List<ClientObject>(); 

        public string[] getOnlineUsers()
        {
            string[] onlineUsers = new string[clients.Count];
            for(int i = 0; i < clients.Count; i++)
            {
                onlineUsers[i] = clients[i].userName;
            }
            return onlineUsers;
        }

        protected internal void AddConnection(ClientObject clientObject)
        {
            clients.Add(clientObject);
        }
        protected internal void RemoveConnection(string id)
        {
            ClientObject client = clients.FirstOrDefault(c => c.Id == id);
            if (client != null)
                clients.Remove(client);
        }
        protected internal void Listen()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8888);
                tcpListener.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (true)
                {
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();

                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    Thread clientThread = new Thread(new ThreadStart(clientObject.Process));
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Disconnect();
            }
        }
        
        protected internal void BroadcastMessage(string message, string id)
        {
            byte[] data = Encoding.Unicode.GetBytes(message);
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i].Id != id) 
                {
                    clients[i].Stream.Write(data, 0, data.Length); 
                }
            }
        }

        protected internal void Disconnect()
        {
            tcpListener.Stop(); 

            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].Close(); 
            }
            Environment.Exit(0); 
        }
    }

    class Program
    {
        static ServerObject server; 
        static Thread listenThread;

        static void Main(string[] args)
        {

            try
            {
                server = new ServerObject();
                listenThread = new Thread(new ThreadStart(server.Listen));
                listenThread.Start();
            }
            catch (Exception ex)
            {
                server.Disconnect();
                Console.WriteLine(ex.Message);
            }

            //Console.ReadKey();
        }
    }
}