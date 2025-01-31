﻿using IPXRelayDotNet;

namespace LANCommander.Services
{
    public class IPXRelayService : BaseService
    {
        private IPXRelay Relay;

        public IPXRelayService()
        {
            if (Relay == null)
                Relay = new IPXRelay();

            Init();
        }

        public void Init()
        {
            var settings = SettingService.GetSettings();

            if (Relay != null)
                Stop();

            if (Relay == null)
                Relay = new IPXRelay(settings.IPXRelay.Port);

            if (!settings.IPXRelay.Logging)
                Relay.DisableLogging();

            if (settings.IPXRelay.Enabled)
                Relay.StartAsync();
        }

        public void Stop()
        {
            if (Relay != null)
                Relay.Dispose();

            Relay = null;
        }
    }
}
