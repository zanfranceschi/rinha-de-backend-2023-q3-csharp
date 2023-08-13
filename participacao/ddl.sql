CREATE TABLE public.pessoas (
	id UUID PRIMARY KEY NOT NULL,
	apelido VARCHAR(32) UNIQUE NOT NULL,
	nome VARCHAR(100) NOT NULL,
	nascimento DATE NOT NULL,
	stack TEXT NULL
);

CREATE INDEX pessoas_nome_idx ON public.pessoas (nome);
CREATE INDEX pessoas_stack_idx ON public.pessoas (stack);
