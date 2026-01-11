using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Handler;

namespace RecruitAutomation.Browser.JobFetcher
{
    /// <summary>
    /// Boss岗位请求处理器
    /// 用于拦截Boss直聘的岗位接口响应
    /// </summary>
    public class BossJobRequestHandler : RequestHandler
    {
        private readonly string _accountId;
        private readonly BossJobApiInterceptor _interceptor;

        public BossJobRequestHandler(string accountId, BossJobApiInterceptor interceptor)
        {
            _accountId = accountId;
            _interceptor = interceptor;
        }

        protected override IResourceRequestHandler? GetResourceRequestHandler(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            bool isNavigation,
            bool isDownload,
            string requestInitiator,
            ref bool disableDefaultHandling)
        {
            var url = request.Url;
            // 只拦截真正的岗位列表接口（不是统计接口）
            if (IsJobListApiRequest(url))
            {
                return new BossJobResourceRequestHandler(_accountId, _interceptor, url);
            }

            return base.GetResourceRequestHandler(chromiumWebBrowser, browser, frame, request, 
                isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
        }

        /// <summary>
        /// 判断是否是岗位列表接口（精确匹配）
        /// </summary>
        private bool IsJobListApiRequest(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            // 必须包含岗位列表特征
            var isJobApi = url.Contains("/wapi/zpboss/job/list") ||
                           url.Contains("/wapi/zpgeek/job/list") ||
                           url.Contains("/job/list") ||
                           url.Contains("/job/manage") ||
                           (url.Contains("job") && url.Contains("list"));

            // 排除统计接口
            var isStatApi = url.Contains("/stat") ||
                            url.Contains("/count") ||
                            url.Contains("/summary") ||
                            url.Contains("/init");

            return isJobApi && !isStatApi;
        }
    }

    /// <summary>
    /// 岗位资源请求处理器
    /// </summary>
    public class BossJobResourceRequestHandler : ResourceRequestHandler
    {
        private readonly string _accountId;
        private readonly BossJobApiInterceptor _interceptor;
        private readonly string _url;
        private MemoryStream? _responseStream;
        private string _contentEncoding = "";

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecruitAutomation", "logs", "job_handler.log");

        public BossJobResourceRequestHandler(string accountId, BossJobApiInterceptor interceptor, string url)
        {
            _accountId = accountId;
            _interceptor = interceptor;
            _url = url;
            Log($"[{accountId}] 准备拦截: {url}");
        }

        private void Log(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        protected override CefReturnValue OnBeforeResourceLoad(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IRequestCallback callback)
        {
            return CefReturnValue.Continue;
        }

        protected override IResponseFilter? GetResourceResponseFilter(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response)
        {
            var contentType = response.MimeType ?? "";
            
            // 记录 Content-Encoding
            _contentEncoding = "";
            if (response.Headers != null)
            {
                _contentEncoding = response.Headers["Content-Encoding"] ?? "";
            }
            
            Log($"[{_accountId}] 响应: ContentType={contentType}, Encoding={_contentEncoding}");

            // 只处理 JSON 响应
            if (contentType.Contains("json") || contentType.Contains("text") || string.IsNullOrEmpty(contentType))
            {
                _responseStream = new MemoryStream();
                return new StreamResponseFilter(_responseStream);
            }

            return null;
        }

        protected override void OnResourceLoadComplete(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response,
            UrlRequestStatus status,
            long receivedContentLength)
        {
            if (_responseStream != null && _responseStream.Length > 0)
            {
                try
                {
                    _responseStream.Position = 0;
                    var rawBytes = _responseStream.ToArray();
                    
                    Log($"[{_accountId}] 原始数据长度: {rawBytes.Length}, Encoding: {_contentEncoding}");

                    // 解压数据
                    string responseBody;
                    try
                    {
                        responseBody = DecompressResponse(rawBytes, _contentEncoding);
                    }
                    catch (Exception ex)
                    {
                        Log($"[{_accountId}] 解压失败，尝试直接读取: {ex.Message}");
                        responseBody = Encoding.UTF8.GetString(rawBytes);
                    }

                    Log($"[{_accountId}] 解压后长度: {responseBody.Length}");
                    
                    if (responseBody.Length > 0 && responseBody.Length < 500)
                    {
                        Log($"[{_accountId}] 响应内容: {responseBody}");
                    }
                    else if (responseBody.Length >= 500)
                    {
                        Log($"[{_accountId}] 响应内容(前500): {responseBody.Substring(0, 500)}...");
                    }

                    // 通知拦截器
                    _interceptor.OnResponseReceived(_accountId, _url, responseBody);
                }
                catch (Exception ex)
                {
                    Log($"[{_accountId}] 处理响应失败: {ex.Message}");
                }
                finally
                {
                    _responseStream.Dispose();
                    _responseStream = null;
                }
            }

            base.OnResourceLoadComplete(chromiumWebBrowser, browser, frame, request, response, status, receivedContentLength);
        }

        /// <summary>
        /// 解压响应数据（支持 gzip 和 brotli）
        /// </summary>
        private string DecompressResponse(byte[] data, string encoding)
        {
            if (string.IsNullOrEmpty(encoding))
            {
                return Encoding.UTF8.GetString(data);
            }

            encoding = encoding.ToLower();

            if (encoding.Contains("br") || encoding.Contains("brotli"))
            {
                // Brotli 解压
                using var input = new MemoryStream(data);
                using var brotli = new BrotliStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                brotli.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            else if (encoding.Contains("gzip"))
            {
                // GZip 解压
                using var input = new MemoryStream(data);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            else if (encoding.Contains("deflate"))
            {
                // Deflate 解压
                using var input = new MemoryStream(data);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }

            return Encoding.UTF8.GetString(data);
        }

        public void Cleanup()
        {
            _responseStream?.Dispose();
        }
    }

    /// <summary>
    /// 流响应过滤器 - 用于捕获响应内容
    /// </summary>
    public class StreamResponseFilter : IResponseFilter
    {
        private readonly MemoryStream _stream;

        public StreamResponseFilter(MemoryStream stream)
        {
            _stream = stream;
        }

        public bool InitFilter()
        {
            return true;
        }

        public FilterStatus Filter(
            Stream dataIn,
            out long dataInRead,
            Stream dataOut,
            out long dataOutWritten)
        {
            dataInRead = 0;
            dataOutWritten = 0;

            if (dataIn == null)
            {
                return FilterStatus.Done;
            }

            var buffer = new byte[dataIn.Length];
            var bytesRead = dataIn.Read(buffer, 0, buffer.Length);
            dataInRead = bytesRead;

            if (bytesRead > 0)
            {
                // 写入到输出流（保持原始响应）
                dataOut.Write(buffer, 0, bytesRead);
                dataOutWritten = bytesRead;

                // 同时写入到我们的捕获流
                _stream.Write(buffer, 0, bytesRead);
            }

            return FilterStatus.Done;
        }

        public void Dispose()
        {
        }
    }
}
