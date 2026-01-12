using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;

/// <summary>
/// Интерфейс для шагов, у которых есть собственный блок в PLC.
/// Используется для тегов Selected/Error/End на уровне блока.
/// </summary>
public interface IHasPlcBlock : ITestStep, IHasPlcBlockPath
{
    // PlcBlockPath наследуется от IHasPlcBlockPath
}
