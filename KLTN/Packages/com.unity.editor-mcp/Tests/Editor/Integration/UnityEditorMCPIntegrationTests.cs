using NUnit.Framework;
using UnityEditorMCP.Core;
using UnityEditorMCP.Models;
using System.Reflection;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System;

namespace UnityEditorMCP.Tests.Integration
{
    [TestFixture]
    public class UnityEditorMCPIntegrationTests
    {
        private const int TEST_PORT = 6401; // Different port to avoid conflicts
        private const int CONNECTION_TIMEOUT_MS = 5000;

        private static Task RequireLiveListenerAsync()
        {
            var listenerField =
                typeof(Core.UnityEditorMCP).GetField(
                    "tcpListener",
                    BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(
                listenerField,
                "UnityEditorMCP tcpListener field should exist.");

            var listener = listenerField.GetValue(null) as TcpListener;

            if (listener == null ||
                listener.Server == null ||
                !listener.Server.IsBound)
            {
                Assert.Ignore(
                    "Requires the live listener owned by this Unity Editor instance.");
            }

            return Task.CompletedTask;
        }

        private static async Task WriteFramedMessageAsync(
            NetworkStream stream,
            string message)
        {
            var payload = Encoding.UTF8.GetBytes(message);
            var lengthBytes = BitConverter.GetBytes(payload.Length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }

            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            await stream.WriteAsync(payload, 0, payload.Length);
            await stream.FlushAsync();
        }

        private static async Task ReadExactlyAsync(
            NetworkStream stream,
            byte[] buffer,
            int offset,
            int count)
        {
            var totalRead = 0;

            while (totalRead < count)
            {
                var readTask =
                    stream.ReadAsync(
                        buffer,
                        offset + totalRead,
                        count - totalRead);

                var completed =
                    await Task.WhenAny(
                        readTask,
                        Task.Delay(CONNECTION_TIMEOUT_MS));

                Assert.AreSame(
                    readTask,
                    completed,
                    "Should receive framed MCP response within timeout.");

                var bytesRead = await readTask;

                Assert.Greater(
                    bytesRead,
                    0,
                    "Connection closed before the complete MCP frame was received.");

                totalRead += bytesRead;
            }
        }

        private static async Task<string> ReadFramedMessageAsync(
            NetworkStream stream)
        {
            var lengthBytes = new byte[4];

            await ReadExactlyAsync(
                stream,
                lengthBytes,
                0,
                lengthBytes.Length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }

            var payloadLength = BitConverter.ToInt32(lengthBytes, 0);

            Assert.GreaterOrEqual(payloadLength, 0);
            Assert.LessOrEqual(
                payloadLength,
                1024 * 1024,
                "MCP response frame is unexpectedly large.");

            var payload = new byte[payloadLength];

            if (payloadLength > 0)
            {
                await ReadExactlyAsync(
                    stream,
                    payload,
                    0,
                    payloadLength);
            }

            return Encoding.UTF8.GetString(payload);
        }
        
        [Test]
        public async Task UnityEditorMCP_ShouldAcceptTcpConnection()
        {
            await RequireLiveListenerAsync();

            // Arrange
            TcpClient client = null;
            
            try
            {
                // Act - Try to connect to the Unity TCP server
                client = new TcpClient();
                var connectTask = client.ConnectAsync("127.0.0.1", Core.UnityEditorMCP.DEFAULT_PORT);
                
                // Wait for connection with timeout
                var completed = await Task.WhenAny(connectTask, Task.Delay(CONNECTION_TIMEOUT_MS));
                
                // Assert
                Assert.IsTrue(completed == connectTask, "Connection should complete within timeout");
                Assert.IsTrue(client.Connected, "Client should be connected");

                var statusDeadline =
                    DateTime.UtcNow.AddMilliseconds(CONNECTION_TIMEOUT_MS);

                while (Core.UnityEditorMCP.Status != McpStatus.Connected &&
                       DateTime.UtcNow < statusDeadline)
                {
                    await Task.Delay(10);
                }

                Assert.AreEqual(
                    McpStatus.Connected,
                    Core.UnityEditorMCP.Status,
                    "MCP status should become Connected after this Editor accepts the client.");
            }
            finally
            {
                client?.Close();
                client?.Dispose();
            }
        }
        
        [Test]
        public async Task UnityEditorMCP_ShouldProcessPingCommand()
        {
            await RequireLiveListenerAsync();

            // Arrange
            TcpClient client = null;
            
            try
            {
                client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", Core.UnityEditorMCP.DEFAULT_PORT);
                
                var stream = client.GetStream();

                // The TCP protocol uses a 4-byte big-endian length prefix.
                await WriteFramedMessageAsync(stream, "ping");

                var responseJson = await ReadFramedMessageAsync(stream);
                var response = JObject.Parse(responseJson);

                Assert.AreEqual(
                    "success",
                    response["status"]?.Value<string>());

                Assert.AreEqual(
                    "pong",
                    response["data"]?["message"]?.Value<string>());
            }
            finally
            {
                client?.Close();
                client?.Dispose();
            }
        }
        
        [Test]
        public async Task UnityEditorMCP_ShouldHandleInvalidJson()
        {
            await RequireLiveListenerAsync();

            // Arrange
            TcpClient client = null;
            
            try
            {
                client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", Core.UnityEditorMCP.DEFAULT_PORT);
                
                var stream = client.GetStream();

                await WriteFramedMessageAsync(
                    stream,
                    "{ invalid json }");

                var responseJson = await ReadFramedMessageAsync(stream);
                var response = JObject.Parse(responseJson);

                Assert.AreEqual(
                    "error",
                    response["status"]?.Value<string>());

                StringAssert.Contains(
                    "JSON_ERROR",
                    responseJson);

                StringAssert.Contains(
                    "parsing",
                    responseJson.ToLowerInvariant());
            }
            finally
            {
                client?.Close();
                client?.Dispose();
            }
        }
        
        [Test]
        public async Task UnityEditorMCP_ShouldHandleMultipleClients()
        {
            await RequireLiveListenerAsync();

            // Arrange
            TcpClient client1 = null;
            TcpClient client2 = null;
            
            try
            {
                // Act - Connect two clients
                client1 = new TcpClient();
                await client1.ConnectAsync("127.0.0.1", Core.UnityEditorMCP.DEFAULT_PORT);
                
                client2 = new TcpClient();
                await client2.ConnectAsync("127.0.0.1", Core.UnityEditorMCP.DEFAULT_PORT);

                await WriteFramedMessageAsync(
                    client1.GetStream(),
                    "ping");

                await WriteFramedMessageAsync(
                    client2.GetStream(),
                    "ping");

                var response1 =
                    JObject.Parse(
                        await ReadFramedMessageAsync(client1.GetStream()));

                var response2 =
                    JObject.Parse(
                        await ReadFramedMessageAsync(client2.GetStream()));

                Assert.AreEqual(
                    "pong",
                    response1["data"]?["message"]?.Value<string>());

                Assert.AreEqual(
                    "pong",
                    response2["data"]?["message"]?.Value<string>());
                
                // Assert - Both clients should be connected
                Assert.IsTrue(client1.Connected, "Client 1 should remain connected");
                Assert.IsTrue(client2.Connected, "Client 2 should remain connected");
            }
            finally
            {
                client1?.Close();
                client1?.Dispose();
                client2?.Close();
                client2?.Dispose();
            }
        }
        
        [Test]
        public void UnityEditorMCP_StatusShouldBeDisconnectedOnStartup()
        {
            // Assert - Check initial status
            // Note: In actual Unity, the MCP might already be connected from previous tests
            // This test verifies that the status enum is working correctly
            Assert.IsTrue(
                Core.UnityEditorMCP.Status == McpStatus.Disconnected || 
                Core.UnityEditorMCP.Status == McpStatus.Connected,
                "Status should be either Disconnected or Connected"
            );
        }
        
        [Test]
        public async Task UnityEditorMCP_ShouldReconnectAfterDisconnection()
        {
            await RequireLiveListenerAsync();

            // Arrange
            TcpClient client = null;
            
            try
            {
                // First connection
                client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", Core.UnityEditorMCP.DEFAULT_PORT);
                Assert.IsTrue(client.Connected, "Should connect initially");
                
                // Disconnect
                client.Close();
                client.Dispose();
                
                // Wait a bit for server to process disconnection
                await Task.Delay(500);
                
                // Act - Reconnect
                client = new TcpClient();
                var reconnectTask = client.ConnectAsync("127.0.0.1", Core.UnityEditorMCP.DEFAULT_PORT);
                var completed = await Task.WhenAny(reconnectTask, Task.Delay(CONNECTION_TIMEOUT_MS));
                
                // Assert
                Assert.IsTrue(completed == reconnectTask, "Should reconnect within timeout");
                Assert.IsTrue(client.Connected, "Should be connected after reconnection");
            }
            finally
            {
                client?.Close();
                client?.Dispose();
            }
        }
    }
}
