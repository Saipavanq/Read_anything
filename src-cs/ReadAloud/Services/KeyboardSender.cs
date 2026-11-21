using System.Windows.Forms;

namespace ReadAloud.Services;

internal sealed class KeyboardSender : IKeyboardSender
{
    public void SendCtrlC() => SendKeys.SendWait("^c");
}
