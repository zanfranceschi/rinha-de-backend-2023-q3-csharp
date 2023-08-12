CREATE TABLE public.pessoas (
	id VARCHAR(36) PRIMARY KEY,
	apelido VARCHAR(32) UNIQUE NOT NULL,
	nome VARCHAR(100) NOT NULL,
	nascimento DATE NOT NULL,
	stack JSONB NULL,
	busca TEXT NOT NULL
);

CREATE EXTENSION pg_trgm;
CREATE INDEX pessoas_busca_idx ON public.pessoas USING GIN (busca GIN_TRGM_OPS);
