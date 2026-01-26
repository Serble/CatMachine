namespace CatAssembler;

public enum Register : byte {
    R0 = 0x00,
    R1 = 0x01,
    R2 = 0x02,
    R3 = 0x03,
    R4 = 0x04,
    R5 = 0x05,
    R6 = 0x06,
    R7 = 0x07,
    Sp = 0x08,
    Ip = 0x09,
    Fl = 0x0A,
    It = 0x0B
}

public static class RegisterExtensions {
    
    public static bool TryParse(string str, out Register register) {
        Register? reg = str.ToLower() switch {
            "r0" => Register.R0,
            "r1" => Register.R1,
            "r2" => Register.R2,
            "r3" => Register.R3,
            "r4" => Register.R4,
            "r5" => Register.R5,
            "r6" => Register.R6,
            "r7" => Register.R7,
            "sp" => Register.Sp,
            "ip" => Register.Ip,
            "fl" => Register.Fl,
            "it" => Register.It,
            _ => null
        };
        if (reg is not null) {
            register = reg.Value;
            return true;
        }
        register = default;
        return false;
    }
}
