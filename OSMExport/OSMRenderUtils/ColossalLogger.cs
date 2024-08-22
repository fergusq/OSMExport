using Colossal.Logging;
using OSMRender.Logging;

namespace OSMExport.OSMRenderUtils
{
    internal class ColossalLogger(ILog logger) : ILogger
    {
        private readonly ILog m_Logger = logger;

        public void Debug(string message)
        {
            m_Logger.Debug(message);
        }

        public void Error(string message)
        {
            m_Logger.Error(message);
        }

        public void Info(string message)
        {
            m_Logger.Info(message);
        }

        public void Warning(string message)
        {
            m_Logger.Warn(message);
        }
    }
}
