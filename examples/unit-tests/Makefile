VERFILES=unit1.fst unit2.fst testset.fst short-circuit.fst
include ../Makefile.include
NEGFILES=negative-tests.fst
OTHERFLAGS=--use_eq_at_higher_order

all: .all.ver all-neg

all-neg: $(NEGFILES)
	-$(FSTAR) $(STDLIB) $^
	echo "EXPECT 35 FAILURES<------------------------------------"

