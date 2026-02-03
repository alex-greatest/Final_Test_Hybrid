import re, pathlib
path = pathlib.Path("D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Verification.cs")
text = path.read_text(encoding='utf-8')
pattern = r"(?m)^[ \t]*private async Task<TestStepResult> (ReadAndVerify\w+)\(TestStepContext context, CancellationToken ct\)"
methods = []
for m in re.finditer(pattern, text):
    start = m.start()
    nm = re.search(pattern, text[m.end():])
    end = m.end() + nm.start() if nm else len(text)
    block = text[start:end]
    name = m.group(1)
    getvals = re.findall(r"GetValue<([^>]+)>\(([^)]+)\)", block)
    readm = re.search(r"DiagReader\.(Read\w+Async)\(", block)
    read_method = readm.group(1) if readm else ''
    addr = re.search(r"address = \(ushort\)\(([^\s]+) - _settings.BaseAddressOffset\)", block)
    register = addr.group(1) if addr else ''
    plus = re.search(r"(Register\w+) \+ (\d+)", block)
    register_range = f"{register}..{plus.group(1)}+{plus.group(2)}" if plus else register
    pnam = re.search(r"parameterName:\s*([^,\r\n]+)", block)
    param_name = pnam.group(1).strip() if pnam else ''
    unitm = re.search(r"unit:\s*\"(.*?)\"", block)
    unit = unitm.group(1) if unitm else ''
    addm = re.search(r"testResultsService\.Add\((.*?)\);", block, re.S)
    add_block = addm.group(1) if addm else ''
    valm = re.search(r"value:\s*([^,\r\n]+)", add_block)
    value_expr = valm.group(1).strip() if valm else ''
    errm = re.search(r"errors:\s*\[([^\]]+)\]", block)
    err_def = errm.group(1).strip() if errm else ''
    if 'boilerState.Article' in block:
        typ = 'string'
        keys = 'BoilerState.Article (min/max ?????)'
        fmt = 'string/null->""'
    else:
        typ = getvals[0][0] if getvals else ''
        keys = ' / '.join(k for _, k in getvals) if getvals else ''
        fmt = 'F3' if 'F3' in value_expr else 'ToString()'
    methods.append([name, register_range, read_method, typ, keys, param_name, unit, err_def, fmt])

headers = ['Method','Register','Read','Type','RecipeKeys','ResultName','Unit','ErrorDef','Format']
print('|' + ' | '.join(headers) + '|')
print('|' + ' | '.join(['---']*len(headers)) + '|')
for row in methods:
    print('|' + ' | '.join(row) + '|')
