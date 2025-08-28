using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;
using System.IO;
using System.Security;
using System.Reflection;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO.Compression;

namespace CWebServerLib {
    #region Delegates
    public delegate void HttpListenerContextReceivedProc(CWebServerLib.CWebServer sender, HttpListenerContext context, bool isAsync);
    #endregion

    #region Interfaces
    public interface ICWebServer : IDisposable {
        int ServerPort { get; set; }
        int SSLPort { get; set; }
        Encoding StreamEncoding { get; set; }
        string StreamEncodingName { get; set; }
        HttpListener Server { get; }
        bool ServerIsListening { get; }
        bool ServerIgnoresWriteExceptions { get; set; }
        bool UseGZip { get; set; }
        string AppID { get; }
        string SSLCertHash { get; set; }
        string GlobalKey_SSLCertHash { get; set; }
        HttpListenerContext CurrentContext { get; }
        event HttpListenerContextReceivedProc ContextReceived;

        bool serverStart(); bool serverStartEx(int port); bool serverStartEx2(string prefixesStr); bool serverStartEx3(ICollection<string> prefixes); bool serverStop();
        HttpListenerContext getContext(); IAsyncResult beginGetContext(); HttpListenerContext endGetContext(IAsyncResult ar); void signalContextReceivedEvent(HttpListenerContext context, bool isAsync);
		void initServer(); void Dispose(); bool sysConfig(IEnumerable<int> ports, IEnumerable<int> sslPorts);
        string readFromStream(Stream aStream, int length); byte[] readBytesFromStream(Stream aStream, int length); string readFromStreamToEnd(Stream aStream); string readLineFromStream(Stream aStream);
        void writeToStream(Stream aStream, string buffer); void writeBytesToStream(Stream aStream, byte[] buffer);
    }
    #endregion

    #region Classes
    [ComVisible(true), ComDefaultInterface(typeof(ICWebServer)), ClassInterface(ClassInterfaceType.AutoDual)]
    public class CWebServer : CObject40, ICWebServer, IDisposable {
        public const int DefaultServerPort = 8081, MSBufferSize = 512 * 1024, GZipBufferSize = 2 * 1024 * 1024;
        public const string Key_NOGZIP = "NOGZIP2", ContentType_FormData = "application/x-www-form-urlencoded";
        public const string DefaultGlobalKey_SSLCertHash = "SSLCertHash";
        protected string appID, sslCertHash, globalKey_sslCertHash; protected int serverPort, sslPort;
        protected Encoding streamEncoding = Encoding.Default; protected HttpListener server; protected HttpListenerContext currentContext;

        #region Accessors
        public int ServerPort { get { return serverPort; } set { serverPort = value.emptyCoalesce(DefaultServerPort); } }
        public int SSLPort { get { return sslPort; } set { sslPort = value; } }
        public Encoding StreamEncoding { get { return streamEncoding; } set { streamEncoding = (value ?? Encoding.Default); } }
        public string StreamEncodingName { get { return (StreamEncoding == null ? null : StreamEncoding.WebName); } set { StreamEncoding = (value.bosMu() ? null : Encoding.GetEncoding(value)); } }
        public HttpListener Server { get { return server; } }
        public bool ServerIsListening { get { return Server.IsListening; } }
        public bool ServerIgnoresWriteExceptions { get { return Server.IgnoreWriteExceptions; } set { Server.IgnoreWriteExceptions = value; } }
        public bool UseGZip { get; set; }
        public string AppID {
            get {
                if (appID == null) { appID = CGlobals.getAppID(Assembly.GetExecutingAssembly()).emptyCoalesceB(() => CGlobals.getAppID(Assembly.GetEntryAssembly())); }
                return appID;
            }
        }
        public string SSLCertHash {
            get {
                if (sslCertHash == null) {
                    var value = CGlobals.g.VioGlobals.atIfAbsent(GlobalKey_SSLCertHash, () => CGlobals.g.VioGlobalsOrtak.atIfAbsent(GlobalKey_SSLCertHash));
                    if (value.bosDegilMi()) {
                        var sb = new StringBuilder(value.Length);
                        if (value.bosDegilMi()) {
                            foreach (var ch in value) { if ((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F')) { sb.Append(ch); } }
                            sslCertHash = sb.ToString();
                        }
                    }
                }
                return sslCertHash;
            }
            set { sslCertHash = value; }
        }
        public string GlobalKey_SSLCertHash { get { return globalKey_sslCertHash; } set { globalKey_sslCertHash = value; } }
        public HttpListenerContext CurrentContext { get { return currentContext; } }
        #endregion

        #region Eventler
        public event HttpListenerContextReceivedProc ContextReceived;
        #endregion

        #region Server Interface
        public bool serverStart() {
            var liste = new CList<string>();
            if (serverPort.bosMu() && sslPort.bosMu()) { serverPort = DefaultServerPort; }
            if (serverPort.bosDegilMi()) { liste.Add(string.Format("http://*:{0}/", serverPort)); }
            if (sslPort.bosDegilMi()) { liste.Add(string.Format("https://*:{0}/", sslPort)); }
            return serverStartEx3(liste);
        }
        public bool serverStartEx(int port) { return serverStartEx3(new[] { string.Format("http://*:{0}/", port) }); }
        public bool serverStartEx2(string prefixesStr) { return serverStartEx3(prefixesStr.asLines(boslukAlmaMi: true)); }
        public bool serverStartEx3(ICollection<string> prefixes) {
            if (ServerIsListening) { return false; }
            var ports = new HashSet<int>(); var sslPorts = new HashSet<int>();
            if (prefixes.bosDegilMi()) {
                server.Prefixes.Clear();
                foreach (var _prefix in prefixes) {
                    var prefix = _prefix; if (prefix.bosDegilMi()) { prefix = prefix.Trim(); }
                    if (prefix.bosMu()) { continue; }
                    if (!prefix.EndsWith("/")) { prefix += "/"; }
                    prefix = prefix.Replace("0.0.0.0", "+"); prefix = prefix.Replace("*", "+");
                    server.Prefixes.Add(prefix);
                    var httpsmi = prefix.StartsWith("https://"); var parts = prefix.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1) {
                        var part = parts.last(); if (part.EndsWith("/")) { part = part.Substring(0, part.Length - 1); }
                        var port = part.toInt32(); if (httpsmi) { sslPorts.Add(port); } else { ports.Add(port); }
                    }
                }
            }
            sysConfig(ports, sslPorts); server.Start(); return true;
        }
        public bool serverStop() {
            if (!ServerIsListening) { return false; }
            currentContext = null; server.Stop(); return true;
        }
        public HttpListenerContext getContext() { return server.GetContext(); }
        public IAsyncResult beginGetContext() { currentContext = null; return server.BeginGetContext(httpContextReceived, null); }
        public HttpListenerContext endGetContext(IAsyncResult ar) { try { return server.EndGetContext(ar); } catch (Exception ex) { return null; } }
        public void signalContextReceivedEvent(HttpListenerContext context, bool isAsync) { if (ContextReceived != null) { ContextReceived(this, context, isAsync); } }

        public string readFromStream(Stream aStream, int length) {
            var buffer = new char[length]; var bytes = new StreamReader(aStream, (StreamEncoding ?? Encoding.Default)).Read(buffer, 0, buffer.Length);
            if (bytes < 0) { return null; }
            if (bytes == 0) { return string.Empty; }
            return buffer.toString();
        }
        public byte[] readBytesFromStream(Stream aStream, int length) {
            var buffer = new byte[length]; var bytes = aStream.Read(buffer, 0, buffer.Length);
            if (bytes < 0) { return null; }
            if (bytes == 0) { return new byte[0]; }
            return buffer;
        }
        public string readFromStreamToEnd(Stream aStream) { return new StreamReader(aStream, StreamEncoding ?? Encoding.Default).ReadToEnd(); }
        public string readLineFromStream(Stream aStream) { return new StreamReader(aStream, StreamEncoding ?? Encoding.Default).ReadLine(); }
        public void writeToStream(Stream aStream, string buffer) {
            var useGZipFlag = this.UseGZip; if (useGZipFlag) {
                var vioGlo = CGlobals.g.VioGlobals; var vioGloOrtak = CGlobals.g.VioGlobalsOrtak;
                if (vioGlo?.atIfAbsent(Key_NOGZIP).toBoolQ() ?? vioGloOrtak?.atIfAbsent(Key_NOGZIP).toBoolQ() ?? false) { useGZipFlag = false; }
            }
            using (var ms = new MemoryStream(MSBufferSize)) {
                var gSrm = useGZipFlag ? new GZipStream(ms, CompressionLevel.Optimal, true) : null;
                using (var sw = new StreamWriter((Stream)gSrm ?? (Stream)ms, StreamEncoding ?? Encoding.Default, MSBufferSize, true) { AutoFlush = true }) { sw.Write(buffer); }
                if (gSrm != null) { gSrm.Close(); }
                if (CurrentContext != null && useGZipFlag) {
                    var resp = CurrentContext.Response; resp.SendChunked = false; if (!resp.SendChunked) { resp.ContentLength64 = ms.Length; }
                    CurrentContext.Response.AddHeader("Content-Encoding", "gzip");
                }
                ms.Position = 0; ms.CopyTo(aStream, useGZipFlag ? GZipBufferSize : MSBufferSize);
            }
        }
        public void writeBytesToStream(Stream aStream, byte[] buffer) {
            var useGZipFlag = this.UseGZip; if (useGZipFlag) {
                var vioGlo = CGlobals.g.VioGlobals; var vioGloOrtak = CGlobals.g.VioGlobalsOrtak;
                if (vioGlo?.atIfAbsent(Key_NOGZIP).toBoolQ() ?? vioGloOrtak?.atIfAbsent(Key_NOGZIP).toBoolQ() ?? false) { useGZipFlag = false; }
            }
            using (var ms = new MemoryStream(MSBufferSize)) {
                var gSrm = useGZipFlag ? new GZipStream(ms, CompressionLevel.Optimal, true) : null;
                using (var sw = new BinaryWriter((Stream)gSrm ?? (Stream)ms, StreamEncoding ?? Encoding.Default, true)) { sw.Write(buffer); }
                if (gSrm != null) { gSrm.Close(); }
                if (CurrentContext != null && useGZipFlag) {
                    var resp = CurrentContext.Response; resp.SendChunked = false; if (!resp.SendChunked) { resp.ContentLength64 = ms.Length; }
                    CurrentContext.Response.AddHeader("Content-Encoding", "gzip");
                }
                ms.Position = 0; ms.CopyTo(aStream, useGZipFlag ? GZipBufferSize : MSBufferSize);
            }
        }
        #endregion
        #region Server: Eventler
        protected void httpContextReceived(IAsyncResult ar) {
            bool /* isCompleted = true, */ isAsync = false;
            if (ar != null) { /* isCompleted = ar.IsCompleted ;*/ isAsync = !ar.CompletedSynchronously; currentContext = endGetContext(ar); }
            if (currentContext != null) { signalContextReceivedEvent(currentContext, isAsync); }
        }
		#endregion
		#region Yardimci
		public bool sysConfig(IEnumerable<int> ports, IEnumerable<int> sslPorts) {
            if (ports.bosMu()) { return false; }
			var result = true; var appID = AppID; var hash = SSLCertHash;
            var appFile = CPath.System32.pathCombine("netsh.exe").asFileInfo();
			void islemBlock(IEnumerable<int> _ports, bool httpsmi) {
				var s = httpsmi ? "s" : "";
				foreach (var port in _ports) {
                    /* var appArgs = string.Format( @"http add sslcert ipport=0.0.0.0:{0} certhash={1} appid={{{2}}}", port, hash, appID ); */
                    var argList = new CList<string>(
              $"http delete urlacl url=http{s}://+:{port}/",
                        $"http delete urlacl url=http{s}://*:{port}/",
                        $"http add urlacl url=http{s}://+:{port}/ user=Everyone"
                    );
					if (appID.bosDegilMi() && hash.bosDegilMi()) {
						argList.AddRange(
							$"http delete sslcert ipport=+:{port}",
                            $"http delete sslcert ipport=0.0.0.0:{port}",
							$"http add sslcert ipport=0.0.0.0:{port} appid={{{appID}}} certhash={hash} verifyclientcertrevocation=disable" +
                                $" verifyrevocationwithcachedclientcertonly=disable usagecheck=disable"
						);
					}
					foreach (var appArgs in argList) {
						var _result = appFile.startProcessWith(
                            appArgs, processWaitMSOrZero: 5000, isUseShellExecute: false,
                            isCreateNoWindow: true, windowStyle: ProcessWindowStyle.Minimized
                        );
						result = result && _result;
					}
				}
			}
			if (ports.bosDegilMi()) { islemBlock(ports, false); }
            if (sslPorts.bosDegilMi()) { islemBlock(sslPorts, true); }
			return result;
		}
        #endregion
        #region Not Categorized
        public CWebServer() : base() {
            globalKey_sslCertHash = DefaultGlobalKey_SSLCertHash;
            serverPort = DefaultServerPort;
            initServer();
        }
        [STAThread()]
        public void initServer() {
            Exception lastError = null;
            for (var i = 0; i < 4; i++) {
                try { this.server = new HttpListener() { IgnoreWriteExceptions = true }; break; }
                catch (ThreadAbortException) { Thread.ResetAbort(); return; } catch (ThreadInterruptedException) { return; }
                catch (Exception ex) { lastError = ex; 500.millisecondsWait(); }
            }
            if (lastError != null) { throw lastError; }
        }
        public void Dispose() {
            if (server != null) { serverStop(); server.Close(); server = null; }
            streamEncoding = null; serverPort = sslPort = 0;
        }
        #endregion
    }
    #endregion
}

/*
using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace SSLWebServis
{
    class Program
    {
        static void Main(string[] args)
        {
            string url = "https://external-domain.com:8080/webservice";

            using (HttpListener listener = new HttpListener())
            {
                listener.Prefixes.Add(url + "/");
                listener.Start();
                Console.WriteLine("Web servis çalışıyor...");

                X509Certificate2 certificate = new X509Certificate2("path/to/certificate.pfx", "certificate-password");

                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    // SSL sertifikasını kullanarak güvenli bağlantıyı sağlayın
                    response.HttpListenerContext.Response.ClientCertificate = certificate;

                    // İstek işleme kodu buraya gelecektir

                    response.Close();
                }
            }
        }
    }
}
*/
