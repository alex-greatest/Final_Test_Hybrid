namespace Final_Test_Hybrid.Services.Scanner.RawInput;

public class RawInputMessageFilter(RawInputService service) : IMessageFilter
{
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == RawInputInterop.WM_INPUT)
        {
            service.ProcessRawInput(m.LParam);
        }
        return false;
    }
}
