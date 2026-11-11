using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

class Program
{
    const int REPETICIONES = 5;  

    static void Main(string[] args)
    {
    
        int cantidad = 10_000_000;
        long[] datos = new long[cantidad];

        //creando datos
        for (int i = 0; i < cantidad; i++)
        {
            datos[i] = i + 1;
        }

        Console.WriteLine("TAXONOMIA DE FLYNN EN C#");

        Console.WriteLine($"Cantidad de datos: {cantidad:N0}");
        Console.WriteLine($"Procesadores logicos: {Environment.ProcessorCount}");
        Console.WriteLine($"Soporte SIMD: {Vector.IsHardwareAccelerated}");
        Console.WriteLine($"Elementos por vector: {Vector<long>.Count}");
        Console.WriteLine($"Promedio de {REPETICIONES} ejecuciones por modelo");

        long[] resSISD = new long[cantidad];
        long[] resSIMD = new long[cantidad];
        long[] resMIMD = new long[cantidad];

        // --- SISD ---
        double tSISD = Medir(() => EjecutarSISD(datos, resSISD));
        Console.WriteLine("\n--- MODELO SISD ---");
        MostrarResultados(resSISD, 0, "Primeros 5 resultados (cuadrado):");
        Console.WriteLine($"Tiempo: {tSISD:F4} ms");

        // --- SIMD ----
        double tSIMD = Medir(() => EjecutarSIMD(datos, resSIMD));
        Console.WriteLine("\n--- MODELO SIMD ---");
        MostrarResultados(resSIMD, 0, "Primeros 5 resultados (cuadrado):");
        Console.WriteLine($"Tiempo: {tSIMD:F4} ms");

        // --- MIMD (todos los hilos) --
        int hilos = Environment.ProcessorCount;
        double tMIMD = Medir(() => EjecutarMIMD(datos, resMIMD, hilos));
        Console.WriteLine($"\n--- MODELO MIMD ({hilos} tareas) ---");
        int tamano = datos.Length / hilos;
        MostrarResultados(resMIMD, 0, "Primeros 5 numeros elevados al cuadrado (tarea 0):");
        MostrarResultados(resMIMD, tamano, "Primeros 5 numeros multiplicados por 10 (tarea 1):");
        Console.WriteLine($"Tiempo: {tMIMD:F4} ms");

        Console.WriteLine("\n--- VERIFICACION ---");
        Console.WriteLine($"SISD == SIMD: {SonIguales(resSISD, resSIMD)}");
        Console.WriteLine($"MIMD correcto: {VerificarMIMD(datos, resMIMD, hilos)}");

        //tabla comparativa con speedup respecto a SISD
        Console.WriteLine("\n--- COMPARACION (speedup respecto a SISD) ---");
        Console.WriteLine($"{"Modelo",-14}{"Tiempo (ms)",14}{"Speedup",10}");
        Console.WriteLine($"{"SISD",-14}{tSISD,14:F4}{1.0,9:F2}x");
        Console.WriteLine($"{"SIMD",-14}{tSIMD,14:F4}{tSISD / tSIMD,9:F2}x");

        //escalabilidad de MIMD con distinta cantidad de tareas
        Console.WriteLine("\n--- ESCALABILIDAD MIMD ---");
        Console.WriteLine($"{"Tareas",-14}{"Tiempo (ms)",14}{"Speedup",10}");
        int[] cantidades = { 2, 4, 8, 16 };
        foreach (int n in cantidades)
        {
            double t = Medir(() => EjecutarMIMD(datos, resMIMD, n));
            Console.WriteLine($"{n,-14}{t,14:F4}{tSISD / t,9:F2}x");
        }

        Console.WriteLine("       FIN DEL PROGRAMA");
 
    }

    //SISD: Una instruccion y un dato
    static void EjecutarSISD(long[] datos, long[] resultados)
    {
        for (int i = 0; i < datos.Length; i++)
        {
            resultados[i] = datos[i] * datos[i];
        }
    }

    //SIMD: Una instruccion y multiples datos
    static void EjecutarSIMD(long[] datos, long[] resultados)
    {
        int elementosPorVector = Vector<long>.Count;
        int i = 0;

        //procesamiento de varios datos simultaneamente
        for (; i <= datos.Length - elementosPorVector; i += elementosPorVector)
        {
            Vector<long> vectorDatos = new Vector<long>(datos, i);
            Vector<long> vectorResultado = vectorDatos * vectorDatos;
            vectorResultado.CopyTo(resultados, i);
        }

        //procesando los elementos restantes
        for (; i < datos.Length; i++)
        {
            resultados[i] = datos[i] * datos[i];
        }
    }
    /*
    MIMD: Multiples instrucciones y multiples datos
    Cada tarea recibe su propio trozo de datos y su propia operacion:
    tareas pares -> cuadrado , tareas impares -> multiplicar por 10
    */
    static void EjecutarMIMD(long[] datos, long[] resultados, int numTareas)
    {
        int tamano = datos.Length / numTareas;
        Task[] tareas = new Task[numTareas];

        for (int t = 0; t < numTareas; t++)
        {
            int id = t;   //esta es una copia local para la lambda
            int inicio = id * tamano;
            int fin = (id == numTareas - 1) ? datos.Length : inicio + tamano;
            bool elevarAlCuadrado = (id % 2 == 0);

            tareas[t] = Task.Run(() =>
            {
                if (elevarAlCuadrado)
                {
                    for (int i = inicio; i < fin; i++)
                        resultados[i] = datos[i] * datos[i];
                }
                else
                {
                    for (int i = inicio; i < fin; i++)
                        resultados[i] = datos[i] * 10;
                }
            });
        }

        Task.WaitAll(tareas);
    }

    //Mide una accion: 1 ejecucion de calentamiento (JIT) y promedio de varias
    static double Medir(Action accion)
    {
        accion();   

        double total = 0;
        for (int r = 0; r < REPETICIONES; r++)
        {
            Stopwatch reloj = Stopwatch.StartNew();
            accion();
            reloj.Stop();
            total += reloj.Elapsed.TotalMilliseconds;
        }
        return total / REPETICIONES;
    }

    static bool SonIguales(long[] a, long[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    static bool VerificarMIMD(long[] datos, long[] resultados, int numTareas)
    {
        int tamano = datos.Length / numTareas;
        for (int t = 0; t < numTareas; t++)
        {
            int inicio = t * tamano;
            int fin = (t == numTareas - 1) ? datos.Length : inicio + tamano;
            for (int i = inicio; i < fin; i++)
            {
                long esperado = (t % 2 == 0) ? datos[i] * datos[i] : datos[i] * 10;
                if (resultados[i] != esperado) return false;
            }
        }
        return true;
    }

    //Mostrando 5 resultados a partir de una posicion
    static void MostrarResultados(long[] resultados, int desde, string titulo)
    {
        Console.WriteLine(titulo);
        for (int i = desde; i < desde + 5; i++)
        {
            Console.WriteLine(resultados[i]);
        }
    }
}


/*
En esta práctica se implementaron y compararon los modelos SISD, SIMD y MIMD de la taxonomía de Flynn en modo Release, con 10 
millones de datos y un procesador de 8 núcleos físicos y 16 lógicos. SISD tardó 8.48 ms y sirvió de referencia. MIMD fue el más rápido 
(6.07 ms con 16 tareas, 1.40x), y su mejor resultado fue con 2 tareas (1.62x); el speedup bajó al aumentar las tareas porque la operación 
es tan simple que el rendimiento queda limitado por la memoria y más tareas solo añaden costo de coordinación. SIMD tardó 17.65 ms (0.48x), 
probablemente porque AVX2 no tiene multiplicación nativa de enteros de 64 bits, lo que muestra que su rendimiento depende del tipo de dato y 
del hardware. En conclusión, ningún modelo es el mejor en todos los casos: la elección depende de la operación, el tipo de dato y la arquitectura.
*/