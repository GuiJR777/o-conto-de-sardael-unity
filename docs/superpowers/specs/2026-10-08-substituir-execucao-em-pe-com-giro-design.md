# Substituir execução em pé com giro

## Objetivo

Remover a execução em pé `Attack3_Stage1_Complete`, que faz Sardael girar visualmente ao redor do inimigo antes do golpe, e substituí-la por uma coreografia em pé com menor rotação.

## Diagnóstico

As duas execuções em pé atuais foram medidas diretamente pelas curvas de raiz dos clipes do executor:

- `Attack6_Stage3_Complete`: 4,4 cm de deslocamento planar e 35,5° de rotação máxima.
- `Attack3_Stage1_Complete`: 8,3 cm de deslocamento planar e 48,9° de rotação máxima.

As execuções no chão não apresentam o problema relatado e permanecem inalteradas. Entre os pares alternativos disponíveis, `Attack11` possui executor e reação correspondentes, 11,4 cm de deslocamento planar e 38,9° de rotação máxima.

## Alteração aprovada

- Manter `Attack6_Stage3_Complete` como a primeira execução em pé.
- Substituir `Attack3_Stage1_Complete` por `Attack11` como a segunda execução em pé.
- Usar os controllers IP do executor e da vítima/reação para preservar o alinhamento do par.
- Manter o sorteio atual: duas execuções no chão e duas em pé, sem repetição até esvaziar o conjunto.
- Não modificar as duas execuções no chão nem a lógica geral de execução.

## Sincronização

O clipe `Attack11` possui 2,5 s na velocidade original e não contém evento de impacto. O sistema continuará aplicando a velocidade global de animação de 1,5x. Seguindo a proporção das execuções atuais, o impacto ocorrerá em 1,5 s do clipe e o controller normal será restaurado em 2,4 s. No jogo, isso corresponde a aproximadamente 1,0 s até o impacto e 1,6 s de duração total.

## Validação

- O teste das execuções em pé passará a exigir no máximo 40° de rotação da raiz e 15 cm de deslocamento planar.
- O teste também confirmará que as duas variantes configuradas são `Attack6_Stage3_Complete` e `Attack11`.
- A cena `Combate_Sandbox` será atualizada pelo Unity Editor, sem edição manual do YAML.
- A suíte EditMode deverá passar integralmente e o Play Mode não poderá registrar erros ou avisos novos.
