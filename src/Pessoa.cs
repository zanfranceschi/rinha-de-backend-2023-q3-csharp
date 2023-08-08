using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RinhaDeBackEnd;

public class Pessoa
{
    public Guid? Id { get; set; }

    
    public string? Apelido { get; set; }
    
    public string? Nome { get; set; }
    
    public DateOnly? Nascimento { get; set; }
    
    public IEnumerable<string>? Stack { get; set; }
    
    public override string ToString()
    {
        return $"apelido: {Apelido}, nome: {Nome}, nascimento: {Nascimento}, stack: {Stack}";
    }
}
