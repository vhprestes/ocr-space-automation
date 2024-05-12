# ocr-space-automation

# Guia de uso do projeto

## Introdução

Bem vindo ao projeto de automação para extração de dados em pdf utilizando a OCR space desenvolvido por Victor Hugo Soares Prestes Martins com objetivo de aprender e me aprofundar na linguagem e no consumo de api OCR para criar automações.
<br/>

## Requisitos

- Uma apikey é necessária, mas para fins de executar o projeto para testes, a apikey esta declarada no projeto e o usuário precisa inserir uma nova. Esta apikey pode ser resgatada facilmente no [site da ocr space](https://ocr.space/ocrapi/freekey), para enfim ser substituida no campo apikey do projeto

- Inicialmente, será necessário um ambiente de desenvolvimento configurado com o [.NET Framework](https://dotnet.microsoft.com/en-us/download) (para o desenvolvimento do projeto, foi utilizado a versão 8.0)

- Será necessário também uma IDE, como [Visual Studio Code](https://code.visualstudio.com/download)
  <br/>

## Configuração

- Extraia o projeto em uma nova pasta e execute o arquivo Program.cs utilizando o vs code.

- Como o projeto foi enviado em .zip, as dependências ja se encontram no projeto. Caso haja a necessidade de reinstala-las, basta abrir uma janela to terminal (atalho: Ctrl + Shift + ` ) e executar os comandos:

```bash
   dotnet add package Newtonsoft.Json --version 13.0.3
   dotnet add package System.Net.Http --version 4.3.4
```

- Certifique-se de estar na pasta raiz do projeto ao executar qualquer comando

- É necessário alterar o caminho na linha 11 da string diretório para a pasta aonde estão os PDF a serem lidos

```bash
   string diretorio = @"<INSIRA AQUI O CAMINHO DA PASTA COM SEUS ARQUIVOS PDF>";
```

- Na linha 13 é aonde a APIKEY do desenvolvedor esta e deve ser trocada pela sua.

```bash
   string APIKEY = "<SUA API KEY AQUI>";
```

<br/>
<br/>

## Execução do código

Executar o código utilizando o comando

```bash
   dotnet run
```
