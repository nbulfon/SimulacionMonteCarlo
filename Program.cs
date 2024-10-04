// Importa las bibliotecas necesarias
using MathNet.Numerics.Distributions;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot.WindowsForms;

/* Todos los datos fueron sacados de Yahoo Finance, GroupFocus y Bloomberg.*/

// Configuración de la simulación
int nSimulaciones = 20000;  // Número de simulaciones -- 20 mil. (Valor arbitrario)
int horizonte = 5;  // Horizonte de 5 años

// Parámetros iniciales
List<double> VANs = new List<double>();
double ingresosIniciales = 17108000;  // Ingresos iniciales en millones de USD (ultimo año.2024...)

Random random = new Random();

// Media de 44%, y Moda de 10% (Yahoo Finance).
Normal distribucionCrecimientoIngresos = new Normal(0.4404, 0.10);

List<double> ebitda = new List<double> { 2860000, 2455000, 1504000, 674000, 293000 }; // datos sacados de Yahoo Finance.
List<double> totalRevenue = new List<double> { 17108000, 14473000, 10537000, 7069000, 3974000 }; // datos sacados de Yahoo Finance.

// Margen de utilidad. Utilizo el margen EBITDA (Yahoo Finance)
Triangular distribucionMargenUtilidad = FuncionesSimulacionMonteCarlo.CalcularDistribucionMargenUtilidad(ebitda, totalRevenue);

// Tasa de descuento (uso el WACC de la empresa) -> la saco de gurusFocus.com -> uso el WACC de Meli.
// Dado que el WACC es 12.72%, puedes usar un rango razonable del 10% al 15% para reflejar la posible variabilidad:
var distribucionTasaDescuento = new ContinuousUniform(0.10, 0.15);


// Realizo la simulación Monte Carlo ->
for (int i = 0; i < nSimulaciones; i++)
{
    double ingresos = ingresosIniciales;
    double flujoTotal = 0;

    for (int t = 1; t <= horizonte; t++)
    {
        double tasaCrecimiento = distribucionCrecimientoIngresos.Sample();
        double margenUtilidad = distribucionMargenUtilidad.Sample();
        double tasaDescuento = distribucionTasaDescuento.Sample();

        ingresos *= (1 + tasaCrecimiento);
        double utilidadOperativa = ingresos * margenUtilidad;

        // Calcular el flujo descontado
        flujoTotal += utilidadOperativa / Math.Pow(1 + tasaDescuento, t);
    }

    // Guardar el resultado de la simulación de VAN
    VANs.Add(flujoTotal);
}

// Resultados ->
double mediaVAN = Mean(VANs);
double desviacionEstandarVAN = StandardDeviation(VANs);
Console.WriteLine($"Media del VAN: {mediaVAN:N2} millones de USD");
Console.WriteLine($"Desviación Estándar del VAN: {desviacionEstandarVAN:N2} millones de USD");

// Finalmente, hago un grafico con la distribución del VAN ->
CrearGrafico(VANs);


#region Metodos - Funciones
// Método para calcular la media
double Mean(List<double> valores) => valores.Average();

// Método para calcular la desviación estándar
double StandardDeviation(List<double> valores)
{
    double media = Mean(valores);
    return Math.Sqrt(valores.Average(v => Math.Pow(v - media, 2)));
}

// Método para crear el gráfico de la distribución del VAN
void CrearGrafico(List<double> VANs)
{
    var modelo = new PlotModel { Title = "Distribución del Valor Actual Neto (VAN)" };

    // Configurar los bins manualmente
    int numBins = 50;
    double min = VANs.Min();
    double max = VANs.Max();
    double binWidth = (max - min) / numBins;
    var counts = new List<double>();

    // Generar los elementos del histograma
    for (int i = 0; i < numBins; i++)
    {
        double binStart = min + i * binWidth;
        double binEnd = binStart + binWidth;
        int count = VANs.Count(value => value >= binStart && value < binEnd);
        counts.Add(count);
    }

    // Crear la serie de columnas para el histograma
    var histogramSeries = new HistogramSeries
    {
        Title = "Frecuencia",
        StrokeThickness = 1,
        FillColor = OxyColors.SkyBlue,
        StrokeColor = OxyColors.Black
    };
    // Agregar cada bin a HistogramSeries
    for (int i = 0; i < numBins; i++)
    {
        double binStart = min + i * binWidth;
        double binEnd = binStart + binWidth;
        int count = VANs.Count(value => value >= binStart && value < binEnd);

        // Añadir los datos al histograma
        histogramSeries.Items.Add(new HistogramItem(binStart, binEnd, count, 0));
    }

    // Agregar la serie al modelo
    modelo.Series.Add(histogramSeries);

    // Configurar los ejes
    modelo.Axes.Add(new LinearAxis
    {
        Position = AxisPosition.Bottom,
        Title = "VAN (Millones de USD)",
        LabelFormatter = value => (value / 1_000_000).ToString("0.0") + "M" // Divide el valor por 1,000,000 y lo muestra en millones
    });
    modelo.Axes.Add(new LinearAxis
    {
        Position = AxisPosition.Left,
        Title = "Frecuencia"
    });
    // Mostrar el gráfico en un formulario de Windows Forms
    var ventana = new PlotView
    {
        Dock = DockStyle.Fill,
        Model = modelo
    };

    var form = new Form { Text = "Distribución del VAN", Width = 800, Height = 600 };
    form.Controls.Add(ventana);
    System.Windows.Forms.Application.Run(form);
}
#endregion

public class FuncionesSimulacionMonteCarlo
{
    /// <summary>
    /// función para calcular la distribución del margen de utilidad
    /// </summary>
    /// <param name="ebitda"></param>
    /// <param name="totalRevenue"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static Triangular CalcularDistribucionMargenUtilidad(List<double> ebitda, List<double> totalRevenue)
    {
        if (ebitda.Count != totalRevenue.Count || ebitda.Count == 0)
        {
            throw new ArgumentException("Las listas de EBITDA y Total Revenue deben tener el mismo tamaño y no deben estar vacías.");
        }

        // calculo los márgenes de utilidad año a año
        List<double> margenesUtilidad = new List<double>();
        for (int i = 0; i < ebitda.Count; i++)
        {
            double margen = ebitda[i] / totalRevenue[i];
            margenesUtilidad.Add(margen);
        }

        // calculo la media, mínimo y máximo
        double media = CalcularMedia(margenesUtilidad);
        double minimo = CalcularMinimo(margenesUtilidad);
        double maximo = CalcularMaximo(margenesUtilidad);

        // Crear la distribución triangular
        return new Triangular(minimo, maximo, media);
    }

    /// <summary>
    /// función auxiliar para calcular la media.
    /// </summary>
    /// <param name="valores"></param>
    /// <returns></returns>
    private static double CalcularMedia(List<double> valores)
    {
        double suma = 0;
        foreach (var valor in valores)
        {
            suma += valor;
        }
        return suma / valores.Count;
    }

    /// <summary>
    /// función auxiliar para calcular el valor mínimo.
    /// </summary>
    /// <param name="valores"></param>
    /// <returns></returns>
    private static double CalcularMinimo(List<double> valores)
    {
        double minimo = double.MaxValue;
        foreach (var valor in valores)
        {
            if (valor < minimo)
                minimo = valor;
        }
        return minimo;
    }

    /// <summary>
    /// función auxiliar para calcular el valor máximo.
    /// </summary>
    /// <param name="valores"></param>
    /// <returns></returns>
    private static double CalcularMaximo(List<double> valores)
    {
        double maximo = double.MinValue;
        foreach (var valor in valores)
        {
            if (valor > maximo)
                maximo = valor;
        }
        return maximo;
    }
}
