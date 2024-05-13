using System;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

public class Program
{
    static async Task Main(string[] args)
    {
        string diretorio = @"INSIRA O CAMINHO AQUI";
        // Caminho para aonde estão os PDF
        string APIKEY = "INSIRA AQUI SUA API KEY";
        var url = "https://api.ocr.space/parse/image"; // URL da API OCR Space

        // Chave da API OCR Space

        if (!Directory.Exists(diretorio))
        {
            // Se o diretório não existir, lançar uma exceção e finalizar a aplicação. Não faz sentido continuar com a mesma sem diretorio afinal
            Console.WriteLine("Erro: o caminnho esta vazio ou incorreto. A aplicação será finalizada. Por favor, verifique o diretorio e tente novamente.");
            throw new DirectoryNotFoundException();
        }


        string[] arquivosPdf = Directory.GetFiles(diretorio, "*.pdf");

        foreach (var arquivo in arquivosPdf)
        {
            // Console.WriteLine(arquivo);
            try
            {
                byte[] pdfFile = File.ReadAllBytes(arquivo);

                // >>>>>>>>>>>> A API ESPERA QUE SEJA ENVIADO UM FORM-DATA, E NAO UM BYTE ARRAY DIRETAMENTE <<<<<<<<<<<<<<<
                using (var httpClient = new HttpClient())
                {
                    MultipartFormDataContent form = new MultipartFormDataContent
                    {
                        { new StringContent(APIKEY), "apikey" },
                        { new StringContent("true"), "isOverlayRequired" },
                        { new ByteArrayContent(pdfFile, 0, pdfFile.Length), "file", "file.pdf" }
                    };


                    // Fazendo a requisição
                    var response = await httpClient.PostAsync(url, form);


                    if (response.IsSuccessStatusCode)
                    {
                        // Processar o conteúdo da resposta para manter apenas o texto extraído
                        // var conteudoResposta = await response.Content.ReadAsStringAsync();
                        var jsonResponse = JObject.Parse(response.Content.ReadAsStringAsync().Result);


                        var parsedResults = jsonResponse["ParsedResults"];

                        var combinedText = "";
                        // Concatenar o texto extraído de cada página
                        // cada página é um objeto no array de resultados

                        if (parsedResults == null)
                        {
                            Console.WriteLine($"Erro ao processar {Path.GetFileName(arquivo)}: {jsonResponse["ErrorMessage"]}");
                            continue;
                            // Se não houver resultados, exibir a mensagem de erro e continuar para o próximo arquivo
                        }


                        foreach (var result in parsedResults)
                        {
                            if (result != null)
                            {
                                var ocrText = result["ParsedText"]?.ToString();
                                combinedText += ocrText + "\n";
                            }
                        }


                        Console.WriteLine($"Resposta para {Path.GetFileName(arquivo)}: {combinedText}");

                        // chama a função ExtractData para extrair os dados do texto extraído
                        var data = ExtractData(combinedText, Path.GetFileName(arquivo));

                        // cria o json
                        WriteJsonFile(arquivo, data);

                        // cria um json no mesmo diretorio do pdf, mas o combinedText
                        // string textFileName = Path.ChangeExtension(diretorio, ".json");
                        // File.WriteAllText(textFileName, combinedText);

                    }
                    else
                    {
                        Console.WriteLine($"Erro na requisição para {Path.GetFileName(arquivo)}: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar {Path.GetFileName(arquivo)}: {ex.Message}");
            }
        }

    }

    static Dictionary<string, object> ExtractData(string ocrText, string pdfFileName)
    {
        var data = new Dictionary<string, object>();


        // Dividir o texto extraído em linhas para facilitar a análise e manipulação
        string[] lines = ocrText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Variáveis para armazenar os valores extraídos
        string invoiceNumber = string.Empty;
        string date = string.Empty;
        string billedTo = string.Empty;
        string businessNumber = string.Empty;
        string sumService = string.Empty;
        string dueDate = string.Empty;
        bool foundService = false;
        bool foundDetails = false;
        var content = new HashSet<string>();
        var total = string.Empty;


        //Percorrer as linhas após leitura pelo OCR Space.
        foreach (string line in lines)
        {
            //Delimitar quando achamos o começo e fim da tabela (começo é details -> service, fim payment details)
            if (foundService)
            {
                if (line.IndexOf("Payment details", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }
                content.Add(line.Trim());
            }

            else if (foundDetails)
            {
                foundService = true;
            }

            //Busca pelo começo da tabela
            else if (line.IndexOf("Details", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foundDetails = true;
            }



            for (int i = 0; i < lines.Length; i++)
            {
                string _line = lines[i].Trim();
                if (_line.StartsWith("Billed to", StringComparison.OrdinalIgnoreCase))
                {
                    // Se sim, tentar capturar o nome que vem abaixo
                    billedTo = GetContentFromNextLine(lines, i + 1);

                }
                // test 
                // passed

                if (_line.StartsWith("Business number (in Brazil):", StringComparison.OrdinalIgnoreCase))
                {
                    // Console.WriteLine("ENTROU AQUI");
                    businessNumber = GetContentFromNextLine(lines, i + 1);
                }


                // aqui começo a testar pegar as linhas depois de service
                // Aqui, ele ao percorrer as lines e encontrar a palavra Service, e a partir dela, salvar as informações em alguma estrutura até que a palavra Total apareça, ai essa não deve ser salva
                // Então, da mesma forma, ele tem que fazer quando encontrar a palavra Date from (que deve parar quando aparece date to), date to (que deve parar quando aparece price) e price(que deve parar quando começa payment details). 
                // Cada um salvo em uma estrutura diferente, e depois, salvar tudo isso em um dicionário.


            }

            // usando o método startsWith pra achar a linha que contém cada informação
            if (line.StartsWith("Invoice Number:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    invoiceNumber = line.Substring("Invoice Number:".Length).Trim();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao extrair Invoice Number: {ex.Message}");
                }
            }




            // REMINDER
            // ISO 8601: a parte da data segue o formato YYYY-MM-DD , que aparece como 2017-06-01. 
            // Se um horário ISO estiver incluído, a hora e o fuso horário serão afixados à data depois de um designador T ,
            //  com a aparência de 2016-06-01T14:41:36-08:00.
            else if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string dateString = line.Substring("Date:".Length).Trim();
                    var _date = dateString.Substring(0, Math.Min(dateString.Length, 10));
                    date = DateTime.Parse(_date).ToString("yyyy/MM/dd");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao extrair Date: {ex.Message}");
                }
            }

        }

        //buscar o Due date: partindo do index de Service.
        int serviçoIndex = content.ToList().FindIndex(x => x.StartsWith("Service", StringComparison.OrdinalIgnoreCase));
        if (serviçoIndex != -1)
        {
            content = new HashSet<string>(content.Skip(serviçoIndex));
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // Se a linha começar com "Due date: "
            if (line.StartsWith("Due date: ", StringComparison.OrdinalIgnoreCase))
            {
                // Capturando o conteúdo presente em toda a linha que começa com o solicitado.
                string dueDateLine = line.Substring("Due date: ".Length).Trim();

                if (!string.IsNullOrWhiteSpace(dueDateLine))
                {
                    // Conversão para DateTime para verificar se há horário presente
                    if (DateTime.TryParse(dueDateLine, out DateTime parsedDueDate))
                    {
                        // Verificar se há hora presente na data
                        bool hasTime = parsedDueDate.TimeOfDay != TimeSpan.Zero;

                        // Formatar de acordo com a presença do horário
                        if (hasTime)
                        {
                            dueDate = parsedDueDate.ToString("s"); // Formato ISO8601 completo
                        }
                        else
                        {
                            dueDate = parsedDueDate.ToString("yyyy/MM/dd"); // Apenas a data
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Erro ao converter a data: {dueDateLine}");
                    }
                }
            }

            // date na iso 8601

        }

        int serviceIndex = content.ToList().FindIndex(x => x.StartsWith("Service", StringComparison.OrdinalIgnoreCase));
        if (serviceIndex != -1)
        {
            content = new HashSet<string>(content.Skip(serviceIndex));
        }




        // Identificar o total como string e extrair o valor,
        string totalAsString = content.LastOrDefault();

        int charactersToRemove = 0;
        string moeda = string.Empty;

        if (totalAsString.StartsWith("$"))
        {
            charactersToRemove = 2;
            moeda = "$ US Dollar (USD)";
        }
        else if (totalAsString.StartsWith("R$"))
        {
            charactersToRemove = 3;
            moeda = "R$ Brazilian Real- (BRL)";
        }
        if (charactersToRemove > 0 && totalAsString.Length >= charactersToRemove)
        {
            totalAsString = totalAsString.Substring(charactersToRemove);
        }

        // string numericValue = new(totalAsString.Where(char.IsDigit).ToArray());

        // if (decimal.TryParse(numericValue, out decimal totalValue))
        // {
        //     try
        //     {
        //         //   "Total": "R$4.ooo, o", o valor ta vindo assim, preciso que quando tiver espaços vazios eles sejam removidos e que sempre tenham 2 digitos depois da virgula
        //         string cents = numericValue.Replace(" ", "").Substring(Math.Max(0, numericValue.Length - 2));
        //         string intPart = numericValue.Substring(0, Math.Max(0, numericValue.Length - 2));

        //         data["Total"] = string.Format("{0}.{1}", Convert.ToDecimal(intPart), cents);
        //         data["Moeda"] = moeda;
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"Erro ao extrair Total: {ex.Message}");
        //     }
        // }


        int priceIndex = -1;
        int currentIndex = 0;

        foreach (string line in content)
        {
            if (line.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
            {
                priceIndex = currentIndex;
                break;
            }
            currentIndex++;
        }

        if (priceIndex != -1)
        {
            // Lista para armazenar os valores de preços da tabela, começamos pegando o valor como string e transformando em decimal para tratar e formatar 
            List<decimal> precoVal = new List<decimal>();

            for (int i = priceIndex + 1; i < content.Count - 1; i++)
            {
                string line = content.ElementAt(i);
                string priceValueString = line;     // Converte-os de volta para uma string

                // Formatando o string para remover caracteres indesejados
                priceValueString = priceValueString.Replace(",O", ",00");
                priceValueString = priceValueString.Replace(",OO", ",00");

                priceValueString = new string(priceValueString.Where(char.IsDigit).ToArray());

                if (decimal.TryParse(priceValueString, out decimal priceValue))
                {
                    string cents = priceValueString.Substring(Math.Max(0, priceValueString.Length - 2));
                    string intPart = priceValueString.Substring(0, Math.Max(0, priceValueString.Length - 2));

                    // formatando as milhas e os centavos
                    string formattedPrice = string.Format("{0}.{1}", Convert.ToDecimal(intPart), cents);
                    // é necessario converter novamente pra decimal pra adicionar a lista!
                    precoVal.Add(decimal.Parse(formattedPrice));
                }

            }

            //Foi criada uma lista apenas para os preços formatados
            // Inicializar uma lista para os preços no formato certo
            List<string> precoFormatado = [];

            foreach (decimal preco in precoVal)
            {
                string cents = preco.ToString()[Math.Max(0, preco.ToString().Length - 2)..];
                string intPart = preco.ToString()[..Math.Max(0, preco.ToString().Length - 2)];

                if (cents == "00")
                {
                    cents = ",00";
                }
                else
                {
                    cents = "," + cents;
                }

                // parte inteira + cents e preenche a lista
                string formattedPrice = intPart + cents;
                precoFormatado.Add(formattedPrice);
            }

            // array com os preços formatados
            JArray arrFormatedPrices = [];

            foreach (string preco in precoFormatado)
            {
                // Adicionar a info da moeda antes do preço
                string precoComMoeda = string.Format("{0} {1}", moeda == "R$ - (BRL)" ? "R$" : "$", preco);

                // add cada preço formatado ao array arrFormatedPrices criado previamente
                arrFormatedPrices.Add(new JValue(precoComMoeda));
            }

            // finalmente, adiciono o objeto ao dicionário na key "Preço"
            data["Preço"] = arrFormatedPrices;

            // Agora é necessário somar os preços para comparar com o total no final
            // Converte os preços formatados para valores decimais
            List<decimal> precosDecimais = precoFormatado.Select(p => decimal.Parse(p)).ToList();
            decimal somaPrecosFormatados = precosDecimais.Sum();

            // Obter a parte inteira dos valores e depois obter os centavos (duas últimas casas decimais)
            string intPartSum = ((int)somaPrecosFormatados).ToString("#,##0");
            string centsSum = (somaPrecosFormatados - Math.Truncate(somaPrecosFormatados)).ToString().PadRight(3, '0').Substring(2);

            // Formatar a soma dos preços
            string somaFormatada = string.Format("{0} {1},{2}", moeda == "R$ - (BRL)" ? "R$" : "$", intPartSum, centsSum).Trim();
            data["Soma de Preços"] = somaFormatada;
            sumService = somaFormatada;

        }
        else
        {
            // Se a palavra "Price" não for encontrada, exibir uma mensagem de erro
            // try/catch???
            Console.WriteLine("A palavra Price não foi encontrada");
        }


        // Encontrar o índice da primeira ocorrência da palavra "Price" dentro do Content
        int dateIndex = -1;
        int currentDateIndex = 0;

        foreach (string line in lines)
        {
            if (line.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
            {
                dateIndex = currentDateIndex;
                break;
            }
            currentDateIndex++;
        }


        // Cria uma lista para armazenar os valores de "Date To" no formato ISO8601
        int dateToIndex = lines.ToList().FindIndex(x => x.StartsWith("Date To", StringComparison.OrdinalIgnoreCase));
        if (dateToIndex != -1 && dateIndex != -1)
        {

            List<string> dateToList = new List<string>();

            for (int i = dateToIndex + 1; i < dateIndex; i++)
            {
                string dateToString = lines.ElementAt(i);

                // Conversão da string para DateTime e depois para string no formato ISO8601
                DateTime parsedDate;

                try
                {
                    if (DateTime.TryParse(dateToString, out parsedDate))
                    {
                        // Verificar se há horário presente na data
                        bool hasTime = parsedDate.TimeOfDay != TimeSpan.Zero;

                        // Formatar de acordo com a presença do horário
                        if (hasTime)
                        {
                            dateToList.Add(parsedDate.ToString("s")); // Formato ISO8601 completo
                        }
                        else
                        {
                            dateToList.Add(parsedDate.ToString("yyyy/MM/dd")); // Apenas a data
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao converter a data \"{dateToString}\" para o formato ISO8601: {ex.Message}");
                }

            }
            data["Date To"] = dateToList;
        }
        else
        {
            Console.WriteLine("A palavra \"Date To\" não foi encontrada no conteúdo.");
        }



        // Encontrar o índice da primeira ocorrência da palavra "Price" dentro do Content
        int datePastIndex = -1;
        int DateFromIndex = 0;

        foreach (string line in content)
        {
            if (line.StartsWith("Date to", StringComparison.OrdinalIgnoreCase))
            {
                datePastIndex = DateFromIndex;
                break;
            }
            DateFromIndex++;
        }


        // Cria uma lista para armazenar os valores de "Date From" no formato ISO8601, trabalhando com os indexes
        int datefromIndex = content.ToList().FindIndex(x => x.StartsWith("Date from", StringComparison.OrdinalIgnoreCase));
        if (datefromIndex != -1 && datePastIndex != -1)
        {
            List<string> datefromList = new List<string>();

            for (int i = datefromIndex + 1; i < datePastIndex; i++)
            {
                string dateString = content.ElementAt(i);

                // Conversão de string para DateTime e depois tentar passar pra o ISO 8601
                DateTime parsedDate;
                if (DateTime.TryParse(dateString, out parsedDate))
                {
                    // Verificar se há horário presente na data
                    bool hasTime = parsedDate.TimeOfDay != TimeSpan.Zero;

                    // Formatar de acordo com a presença do horário? Como viria o horario no json, na mesma linha? em outra?
                    if (hasTime)
                    {
                        datefromList.Add(parsedDate.ToString("s")); // Formato ISO8601 completo ?
                    }
                    else
                    {
                        datefromList.Add(parsedDate.ToString("yyyy/MM/dd"));
                    }
                }

                data["Date From"] = datefromList;
            }
        }
        else
        {
            Console.WriteLine("A palavra \"Date From\" não foi encontrada no conteúdo.");
        }


        // Encontrar o índice da primeira ocorrência de total no content
        int serviceDetailsIndex = -1;
        int serviceCurrentIndex = 0;

        foreach (string line in content)
        {
            if (line.StartsWith("Total", StringComparison.OrdinalIgnoreCase))
            {
                serviceDetailsIndex = serviceCurrentIndex;
                break;
            }
            serviceCurrentIndex++;
        }


        // Cria uma lista para armazenar os valores de "Service" até a palavra "Total"
        int servicecurrentIndex = content.ToList().FindIndex(x => x.StartsWith("Details", StringComparison.OrdinalIgnoreCase));
        if (servicecurrentIndex == -1)
        {
            var serviceList = new List<string>();

            for (int i = servicecurrentIndex + 1; i < content.Count; i++)
            {
                string line = content.ElementAt(i);

                if (line.StartsWith("Total", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                serviceList.Add(line);
            }

            data["Service"] = serviceList;
        }


        try
        {
            string totalFormated = content.LastOrDefault();
            totalFormated = totalFormated.Replace(",", "_"); // Substitui ',' por '_'
            totalFormated = totalFormated.Replace(".", ","); // Substitui '.' por ','
            totalFormated = totalFormated.Replace("_", "."); // Substitui '_' por '.'

            total = totalFormated; // Adiciona o símbolo do dólar de volta
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no tratamento de total: {ex.Message}");
        }

        // Adicionar os valores extraídos ao dicionário de dados
        data["Invoice Number"] = invoiceNumber;
        data["Date"] = date;
        data["Due date"] = dueDate;
        data["Billed to"] = billedTo;
        data["Business Number in Brazil"] = businessNumber;
        data["Moeda"] = moeda;
        data["Total"] = total;


        // Calcular se a soma dos preços é igual ao total 
        // é necessário converter o total para decimal para fazer a comparação
        // Exemplo:
        //         "Soma de Preços": "R$ 1.010,00",
        //   "Total": "R$4.ooo, o"
        // é necessario comparar data["Soma de Preços"] com data["Total"]
        // se for igual, adicionar a chave comparador com o valor true, senão, false    
        // string somaPreco = data["Soma de Preços"].ToString().Trim().Replace(" ", "");
        // string sum = data["Total"].ToString().Trim().Replace(" ", "");
        // Console.WriteLine(sumService);
        // Console.WriteLine(total);

        data["A soma dos valores da tabela é igual ao total"] = total == sumService ? true : false;


        // if (somaPreco == sum)
        // {
        //     data["A soma dos valores da tabela é igual ao total"] = true;
        // }
        // else
        // {
        //     data["A soma dos valores da tabela é igual ao total"] = false;
        // }



        return data;
    }


    static void WriteJsonFile(string fileName, Dictionary<string, object> data)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        string jsonFileName = Path.ChangeExtension(fileName, ".json");
        File.WriteAllText(jsonFileName, json);

    }

    static string GetContentFromNextLine(string[] lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }
        return string.Empty;
    }

}

