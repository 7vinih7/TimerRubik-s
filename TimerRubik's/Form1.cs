using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TimerRubik_s
{
    public partial class Form1 : Form
    {
        private class Solve
        {
            public TimeSpan TempoOriginal { get; set; }
            public bool IsDNF { get; set; }
            public bool IsPlusTwo { get; set; }
            public TimeSpan TempoFinal => IsPlusTwo ? TempoOriginal.Add(TimeSpan.FromSeconds(2)) : TempoOriginal;
        }

        private Stopwatch cronometro = new Stopwatch();
        private Stopwatch cronometroInspecao = new Stopwatch();

        private List<Solve> listaDeSolves = new List<Solve>();
        private TimeSpan? ultimoTempoDessaSessao = null;

        private int estadoAtual = 0;
        private string arquivoSalvar = "solves_time .txt";

        private bool espacoPressionado = false;

        public Form1()
        {
            InitializeComponent();
            timer1.Tick += TimerAtualiza_Tick;
            timer1.Interval = 10;

            label2.Text = "";
            this.KeyPreview = true;

            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;

            CarregarDadosDoArquivo();
            ProximoScramble();
            AtualizarInterfaceGeral();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (listBox1.SelectedIndex >= 0)
                {
                    int index = listBox1.SelectedIndex;

                    var listaOrdenada = listaDeSolves
                        .OrderBy(s => s.IsDNF)
                        .ThenBy(s => s.TempoFinal)
                        .ToList();

                    var solveParaRemover = listaOrdenada[index];

                    listaDeSolves.Remove(solveParaRemover);

                    SalvarDadosEmArquivo();
                    AtualizarInterfaceGeral();

                    e.Handled = true;
                    return;
                }
            }

            if (e.KeyCode == Keys.Space)
            {
                e.Handled = true;

                if (espacoPressionado) return;
                espacoPressionado = true;

                if (estadoAtual == 0)
                {
                    estadoAtual = 1;
                    label1.Text = "Pronto";
                    label1.ForeColor = Color.Green;
                }
                else if (estadoAtual == 3)
                {
                    estadoAtual = 4;
                    cronometro.Stop();
                    timer1.Stop();
                    SalvarEOrdenarTempo();
                    ProximoScramble();
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ResetarCronometroSemSalvar();
            }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                espacoPressionado = false;

                if (estadoAtual == 1)
                {
                    estadoAtual = 2;
                    cronometroInspecao.Reset();
                    cronometroInspecao.Start();
                    timer1.Start();
                }
                else if (estadoAtual == 2)
                {
                    estadoAtual = 3;
                    cronometroInspecao.Stop();
                    cronometro.Reset();
                    cronometro.Start();
                    timer1.Start();
                }
                else if (estadoAtual == 4)
                {
                    estadoAtual = 0;
                }
            }
        }

        private void TimerAtualiza_Tick(object sender, EventArgs e)
        {
            if (estadoAtual == 2)
            {
                int segundosRestantes = 15 - (int)cronometroInspecao.Elapsed.TotalSeconds;
                if (segundosRestantes <= 0)
                {
                    label1.Text = "DNF";
                    label1.ForeColor = Color.Red;
                }
                else
                {
                    label1.Text = segundosRestantes.ToString();
                    label1.ForeColor = Color.DarkOrange;
                }
            }
            else if (estadoAtual == 3)
            {
                label1.ForeColor = Color.White;
                label1.Text = FormatarTempo(cronometro.Elapsed);
            }
        }

        private void SalvarEOrdenarTempo()
        {
            if (cronometro.ElapsedTicks > 0)
            {
                TimeSpan tempoAtual = cronometro.Elapsed;

                if (ultimoTempoDessaSessao != null)
                {
                    TimeSpan diferenca = tempoAtual - ultimoTempoDessaSessao.Value;
                    string difFormatada = FormatarTempo(diferenca.Duration());
                    label2.ForeColor = diferenca.TotalMilliseconds < 0 ? Color.Green : Color.Red;
                    label2.Text = (diferenca.TotalMilliseconds < 0 ? "-" : "+") + difFormatada;
                }
                else { label2.Text = ""; }

                ultimoTempoDessaSessao = tempoAtual;

                listaDeSolves.Add(new Solve { TempoOriginal = tempoAtual });
                SalvarDadosEmArquivo();
                AtualizarInterfaceGeral();
            }
        }

        private void AtualizarInterfaceGeral()
        {
            listBox1.Items.Clear();

            var listaOrdenada = listaDeSolves
                .OrderBy(s => s.IsDNF)
                .ThenBy(s => s.TempoFinal)
                .ToList();

            for (int i = 0; i < listaOrdenada.Count; i++)
            {
                var s = listaOrdenada[i];
                string exibicao = s.IsDNF ? "DNF" : FormatarTempo(s.TempoFinal) + (s.IsPlusTwo ? " (+2)" : "");

                if (i == 0 && !s.IsDNF)
                    listBox1.Items.Add($"⭐ PB: {exibicao}");
                else
                    listBox1.Items.Add($"   Pos {i + 1}: {exibicao}");
            }

            CalcularMediasEstatisticas();
        }

        private void ProximoScramble()
        {
            string[] movimentos = { "U", "D", "R", "L", "F", "B" };
            string[] modificadores = { "", "'", "2" };
            Random rand = new Random();
            StringBuilder scramble = new StringBuilder();

            int ultimoMov = -1;
            for (int i = 0; i < 20; i++)
            {
                int mov;
                do { mov = rand.Next(movimentos.Length); } while (mov == ultimoMov);

                ultimoMov = mov;
                scramble.Append(movimentos[mov] + modificadores[rand.Next(modificadores.Length)] + " ");
            }
            label3.Text = scramble.ToString().Trim();
        }

        private void CalcularMediasEstatisticas()
        {
            if (listaDeSolves.Count == 0)
            {
                label4.Text = "Ao5: -\nAo12: -\nMédia Geral: -";
                return;
            }

            var validas = listaDeSolves.Where(s => !s.IsDNF).ToList();
            double mediaGeralMs = validas.Count > 0 ? validas.Average(s => s.TempoFinal.TotalMilliseconds) : 0;

            string ao5Str = "-", ao12Str = "-";

            if (listaDeSolves.Count >= 5)
            {
                var ultimas5 = listaDeSolves.Skip(listaDeSolves.Count - 5).ToList();
                ao5Str = CalcularMediaWCA(ultimas5);
            }
            if (listaDeSolves.Count >= 12)
            {
                var ultimas12 = listaDeSolves.Skip(listaDeSolves.Count - 12).ToList();
                ao12Str = CalcularMediaWCA(ultimas12);
            }

            label4.Text = $"Ao5: {ao5Str}\nAo12: {ao12Str}\nMédia Geral: {FormatarTempo(TimeSpan.FromMilliseconds(mediaGeralMs))}";
        }

        private string CalcularMediaWCA(List<Solve> subLista)
        {
            if (subLista.Count(s => s.IsDNF) > 1) return "DNF";

            var ordenada = subLista.OrderBy(s => s.IsDNF).ThenBy(s => s.TempoFinal.TotalMilliseconds).ToList();
            ordenada.RemoveAt(0);
            ordenada.RemoveAt(ordenada.Count - 1);

            double mediaMs = ordenada.Average(s => s.TempoFinal.TotalMilliseconds);
            return FormatarTempo(TimeSpan.FromMilliseconds(mediaMs));
        }

        private void ResetarCronometroSemSalvar()
        {
            cronometro.Reset();
            cronometroInspecao.Reset();
            timer1.Stop();
            estadoAtual = 0;
            espacoPressionado = false;
            label1.ForeColor = Color.White;
            label1.Text = "00:00.00";
            label2.Text = "";
            ProximoScramble();
        }

        private string FormatarTempo(TimeSpan ts)
        {
            return string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ResetarCronometroSemSalvar();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int index = listBox1.SelectedIndex;
                var listaOrdenada = listaDeSolves.OrderBy(s => s.IsDNF).ThenBy(s => s.TempoFinal).ToList();
                listaOrdenada[index].IsPlusTwo = !listaOrdenada[index].IsPlusTwo;
                SalvarDadosEmArquivo();
                AtualizarInterfaceGeral();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex >= 0)
            {
                int index = listBox1.SelectedIndex;
                var listaOrdenada = listaDeSolves.OrderBy(s => s.IsDNF).ThenBy(s => s.TempoFinal).ToList();
                listaOrdenada[index].IsDNF = !listaOrdenada[index].IsDNF;
                SalvarDadosEmArquivo();
                AtualizarInterfaceGeral();
            }
        }
        private void SalvarDadosEmArquivo()
        {
            System.Collections.Generic.List<string> linhas = new System.Collections.Generic.List<string>();
            foreach (var s in listaDeSolves)
            {
                linhas.Add($"{s.TempoOriginal.TotalMilliseconds};{s.IsPlusTwo};{s.IsDNF}");
            }
            File.WriteAllLines(arquivoSalvar, linhas);
        }

        private void CarregarDadosDoArquivo()
        {
            if (File.Exists(arquivoSalvar))
            {
                string[] linhas = File.ReadAllLines(arquivoSalvar);
                listaDeSolves.Clear();
                foreach (var linha in linhas)
                {
                    var partes = linha.Split(';');
                    if (partes.Length == 3)
                    {
                        listaDeSolves.Add(new Solve
                        {
                            TempoOriginal = TimeSpan.FromMilliseconds(double.Parse(partes[0])),
                            IsPlusTwo = bool.Parse(partes[1]),
                            IsDNF = bool.Parse(partes[2])
                        });
                    }
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {

        }
    }
}
