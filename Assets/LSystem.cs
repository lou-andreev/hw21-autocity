using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public interface IValue
{
    public object Eval(Environment e);
};

public struct Float : IValue
{
    public Float(float x)
    {
        Value = x;
    }

    public static implicit operator Float(float x) => new Float(x);
    public override string ToString()
    {
        return Value.ToString();
    }
    public object Eval(Environment e)
    {
        return Value;
    }
    public float Value;
}

public struct Var : IValue
{
    public Var(string s)
    {
        Name = s;
    }
    public object Eval(Environment e)
    {
        return e.Eval(Name);
    }
    public override string ToString()
    {
        return Name;
    }
    public string Name;
}

public interface ICommand
{
    public bool Try(Module p, ref Environment e);
}

public struct Match : ICommand
{
    public Match(string name, params IValue[] args)
    {
        Name = name;
        Args = args;
    }

    public bool Try(Module p, ref Environment e)
    {
        if (p.Name != Name)
        {
            return false;
        }

        Environment inner = new Environment(e);

        int numArgs = Args.Length;
        for (int i = 0; i < numArgs; i++)
        {
            if (Args[i] is Float)
            {
                if (Args[i] != p.Args[i])
                    return false;
            }
            else
            {
                var v = (Var)Args[i];
                var x = (float)p.Args[i].Eval(e);
                inner.Bind(v.Name, (Float)x);
            }
        }

        if (inner.Context.Count > 0)
        {
            e = inner;
        }
        return true;
    }

    public string Name;
    public IValue[] Args;
}

public interface IPattern
{
    public IPattern[] Try(Production rule, ref Environment e, ref Unity.Mathematics.Random rand);
    public void Interpret(ref Environment e);
    public string Print(ref Environment e);
}

public struct Module : IPattern
{
    public Module(string name, params IValue[] args)
    {
        Name = name;
        Args = args;
    }

    public override string ToString()
    {
        string s = Name;
        if (Args.Length > 0)
        {
            s += "(";
            for (int i = 0; i < Args.Length; i++)
            {
                s += Args[i].ToString() + ",";
            }
            s = s.Take(s.Length - 1) + ")";
        }
        return s;
    }

    public IPattern[] Try(Production rule, ref Environment e, ref Unity.Mathematics.Random rand)
    {
        if (rule.Prob < 1 && rand.NextDouble() >= rule.Prob)
            return null;

        if (rule.Match.Try(this, ref e))
        {
            return rule.Rule(e);
        }

        return null;
    }

    public void Interpret(ref Environment e)
    {
        if (Name == "F")
        {
            Debug.Assert(Args.Length == 1);

            float len = ((Float)Args[0]).Value;
            math.sincos(e.state.angle, out float s, out float c);
            e.state.pos += new float2(s, c) * len;
        }

        else if (Name == "-")
        {
            Debug.Assert(Args.Length == 0);
            e.state.angle += math.PI / 2;
        }

        else if (Name[0] == '?')
        {
            // query
            e.Query(Name.Substring(1), Args.Select(a => (Var)a).ToArray());
            for (int i = 0; i < Args.Length; i++)
            {
                //Args[i] = (Float)((float)Args[i].Eval(e));
            }
        }

    }

    public string Print(ref Environment e)
    {
        string s = Name;

        if (Args.Length > 0)
        {
            s += "(";
            for (int i = 0; i < Args.Length; i++)
            {
                s += Args[i].Eval(e).ToString() + ",";
            }
            s = s.Substring(0, s.Length - 1) + ")";
        }
        return s;
    }

    public string Name;
    public IValue[] Args;
}

public struct Query : IPattern
{
    public delegate void EvalFunc(Var[] args, Environment e);

    public Query(string name, EvalFunc eval, params Var[] args)
    {
        Name = name;
        Args = args;
        Eval = eval;
    }

    public IPattern[] Try(Production rule, ref Environment e, ref Unity.Mathematics.Random rand)
    {
        // query
        Eval(Args, e);
        return new IPattern[] { new Module(Name, Args.Select(a => (IValue)a).ToArray()) };
    }

    public void Interpret(ref Environment e)
    {

    }

    public string Print(ref Environment e)
    {
        return "not implemented";
    }

    public string Name;
    public EvalFunc Eval;
    public Var[] Args;
}

public class Environment
{
    public Environment()
    {
    }
    public Environment(Environment outer)
    {
        Outer = outer;
    }

    public struct State
    {
        public float2 pos;
        public float angle;
    }

    public State state = new State { pos = 0, angle = 0 };

    public Environment Outer = null;
    public Dictionary<string, IValue> Context = new Dictionary<string, IValue>();

    public delegate IValue[] ReservedModule();
    public Dictionary<string, ReservedModule> ReservedModules = new Dictionary<string, ReservedModule>();

    public delegate void Const();
    public Dictionary<string, Const> Consts = new Dictionary<string, Const>();

    public object Eval(string s)
    {
        if (Eval(s, out object x))
        {
            return x;
        }
        throw new System.Exception("unbound variable " + s);
    }

    public bool Eval(string s, out object x)
    {
        if (Context.TryGetValue(s, out IValue value))
        {
            x = value.Eval(this);
            return true;
        }
        if (Outer == null)
        {
            x = 0;
            return false;
        }

        return Outer.Eval(s, out x);
    }

    public bool Eval(string s, out float x)
    {
        if (Eval(s, out object obj))
        {
            if (obj is float)
            {
                x = (float)obj;
                return true;
            }
        }
        x = 0;
        return false;
    }

    public void Bind(string s, IValue x)
    {
        Context[s] = x;
    }

    internal void Query(string name, Var[] args)
    {
        if (ReservedModules.TryGetValue(name, out var module))
        {
            var xs = module();

            for (int i = 0; i < args.Length; i++)
            {
                Bind(args[i].Name, xs[i]);
            }
        }
        else
        {
            Outer.Query(name, args);
        }
    }
}

public struct Axiom
{
    public Axiom(params IPattern[] patterns)
    {
        Patterns = patterns;
    }
    public IPattern[] Patterns;
}

public struct Production
{
    public delegate IPattern[] ProductionRule(Environment e);
    
    
    public Production(Match m, ProductionRule rule, float prob = 1.0f)
    {
        Match = m;
        Rule = rule;
        Prob = prob;
    }

    public IPattern[] Try(Module p, ref Environment e, ref Unity.Mathematics.Random rand)
    {
        if (Prob < 1 && rand.NextDouble() >= Prob)
            return null;

        if (p.Name[0] == '?')
        {
            // query
            e.Query(p.Name.Substring(1), p.Args.Select(a => (Var)a).ToArray());
        }

        if (Match.Try(p, ref e))
        {
            return Rule(e);
        }
        return null;
    }

    public Match Match;
    public ProductionRule Rule;
    public float Prob;
}

public class Rewrite
{
    public void AddProduction(Production p)
    {
        Productions.Add(p);
    }

    public void AddProduction(Match m, Production.ProductionRule rule)
    {
        Productions.Add(new Production(m, rule));
    }

    Environment env = new Environment();
    public void Init()
    {
        env.ReservedModules.Add("P", () => new IValue[] { (Float)env.state.pos.x, (Float)env.state.pos.y });
    }

    public bool Apply()
    {
        int numPats = Patterns.Length;

        bool found = false;

        for (int i = 0; i < numPats; i++)
        {
            foreach (Production rule in Productions)
            {
                var ps = Patterns[i].Try(rule, ref env, ref r);
                if (ps != null)
                {
                    found = true;

                    Patterns = Patterns.Take(i).Concat(ps.Concat(Patterns.Skip(i+1))).ToArray();
                    numPats = Patterns.Length;
                    i += ps.Length - 1;

                    break;
                }
            }
        }

        for (int i = 0; i < numPats; i++)
        {
            Patterns[i].Interpret(ref env);
        }

        string output = "";
        for (int i = 0; i < numPats; i++)
        {
            string s = Patterns[i].Print(ref env);
            output += s;
        }
        Debug.Log(output);

        return found;
    }

    public IPattern[] Patterns;

    public List<Production> Productions = new List<Production>();

    Unity.Mathematics.Random r = new Unity.Mathematics.Random(1);
}

[ExecuteInEditMode]
public class LSystem : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        /*
        float2 pos = new float2(2, 3);
        Module P = new Module(() => new float[] { pos.x, pos.y });

        Environment env = new Environment();

        Query query = new Query("P", "x", "y");

        env.AddModule("P", P);
        query.Eval(env);
        Debug.Log(env.Eval("x"));*/

        //Environment env = new Environment();
        //env.Bind

        float2 pos = 0;
        Environment env = new Environment();

        /*        
                var omega = new Axiom(new Module("A", (Float)1),
                                      new Module("B", (Float)3),
                                      new Module("A", (Float)5));

                // p1 : A(x) -> A(x+1) : 0.4
                Production p1 = new Production(new Match("A", new Var("x")), 
                                        e => { 
                                                e.Eval("x", out float x); 
                                                return new Module[] { new Module("A", (Float)(x + 1)) }; 
                                        }, 0.04f);

                // p2 : A(x) -> B(x-1) : 0.6
                Production p2 = new Production(new Match("A", new Var("x")),
                                        e => {
                                            e.Eval("x", out float x);
                                            return new Module[] { new Module("B", (Float)(x - 1)) };
                                        }, 0.6f);

                // p3 : A(x)<B(y)>A(z) : y < 4 -> B(x+z) [A(y)]
                Production p3 = new Production(new Match("A", new Var("x")),
                                e => {
                                    e.Eval("x", out float x);
                                    return new Module[] { new Module("B", (Float)(x - 1)) };
                                });
                                */

        var omega = new Axiom(new Module("A"));

/*        var builtin = new Production(new Match("?P"),
                            e => new IPattern[]
                            {
                                new Query("?P", (args, e) => {
                                                    e.Bind(args[0].Name, (Float)pos.x);
                                                    e.Bind(args[1].Name, (Float)pos.y); })
                            }); ;*/

        var p1 = new Production(new Match("A"), 
                            e => new IPattern[] 
                            { 
                                new Module("F", (Float)1), 
                                new Module("?P", new Var("x"), new Var("y")),
                                new Module("-"),
                                new Module("A")
                            }
                            );
        var p2 = new Production(new Match("F", new Var("k")),
            e =>
            {
                e.Eval("k", out float k);
                return new IPattern[] { new Module("F", (Float)(k + 1)) };
            });

        var system = new Rewrite();
        system.Init();
//        system.AddProduction(builtin);
        system.AddProduction(p1);
        system.AddProduction(p2);
        system.Patterns = omega.Patterns;
        
        system.Apply();
        system.Apply();
        system.Apply();
        system.Apply();
        system.Apply();
        system.Apply();


        Debug.Log(system.Productions);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
