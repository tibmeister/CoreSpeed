﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreSpeed.Models;

namespace CoreSpeed
{
    interface ICoreSpeedClient
    {
        /// <summary>
        /// Download speedtest.net settings
        /// </summary>
        /// <returns>speedtest.net settings</returns>
        Settings GetSettings();

        /// <summary>
        /// Test latency (ping) to server
        /// </summary>
        /// <returns>Latency in milliseconds (ms)</returns>
        int TestServerLatency(Server server, int retryCount = 3);

        /// <summary>
        /// Test download speed to server
        /// </summary>
        /// <returns>Download speed in Kbps</returns>
        double TestDownloadSpeed(Server server, int simultaniousDownloads = 2, int retryCount = 2);

        /// <summary>
        /// Test upload speed to server
        /// </summary>
        /// <returns>Upload speed in Kbps</returns>
        double TestUploadSpeed(Server server, int simultaniousUploads = 2, int retryCount = 2);
    }
}