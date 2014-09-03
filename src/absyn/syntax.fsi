﻿(*
   Copyright 2008-2014 Nikhil Swamy and Microsoft Research

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*)
#light "off"
module Microsoft.FStar.Absyn.Syntax
(* Type definitions for the core AST *)

open Prims
open Microsoft.FStar
open Microsoft.FStar.Util
open Microsoft.FStar.Range

exception Err of string
exception Error of string * Range.range
exception Warning of string * Range.range
 
type ident = {idText:string;
              idRange:Range.range}
type LongIdent = {ns:list<ident>; 
                  ident:ident; 
                  nsstr:string;
                  str:string}
type lident = LongIdent
type withinfo_t<'a,'t> = {
  v: 'a; 
  sort: 't;
  p: Range.range; 
} 
type var<'t>  = withinfo_t<lident,'t>
type fieldname = lident
type inst<'a> = ref<option<'a>>
type bvdef<'a> = {ppname:ident; realname:ident}
type bvar<'a,'t> = withinfo_t<bvdef<'a>,'t> 
(* Bound vars have a name for pretty printing, 
   and a unique name generated during desugaring. 
   Only the latter is used during type checking.  *)
  
(* Term language *)
type sconst = 
  | Const_unit
  | Const_uint8       of byte
  | Const_bool        of bool
  | Const_int32       of int32
  | Const_int64       of int64
  | Const_char        of char
  | Const_float       of double
  | Const_bytearray   of array<byte> * Range.range 
  | Const_string      of array<byte> * Range.range           (* unicode encoded, F#/Caml independent *)

type memo<'a> = ref<option<'a>>
type typ' =  
  | Typ_btvar    of btvar
  | Typ_const    of ftvar 
  | Typ_fun      of binders * comp                           (* (ai:ki|xi:ti) -> M t' wp *)
  | Typ_refine   of bvvar * typ                              (* x:t{phi} *)
  | Typ_app      of typ * args                               (* args in reverse order *)
  | Typ_lam      of binders * typ                            (* fun (ai|xi:tau_i) => T *)
  | Typ_ascribed of typ * knd                                (* t <: k *)
  | Typ_meta     of meta_t                                   (* Not really in the type language; a way to stash convenient metadata with types *)
  | Typ_uvar     of uvar_t * knd                             (* not present after 1st round tc *)
  | Typ_delayed  of typ * subst * memo<typ>                  (* A delayed substitution---always force it before inspecting the first arg *)
  | Typ_unknown                                              (* not present after 1st round tc *)
and term = either<typ,exp>
and arg = term * bool                                        (* bool marks an explicitly provided implicit arg *)
and args = list<arg>
and binder = either<btvar,bvvar> * bool
and binders = list<binder>                                   (* bool marks implicit binder *)
and typ = syntax<typ',knd>
and comp_typ = {
  effect_name:lident; 
  result_typ:typ; 
  effect_args:list<either<typ,exp>>;
  flags:list<cflags>
  }
and comp' = 
  | Total of typ
  | Comp of comp_typ                    
  | Rigid of typ                                             (* a type with Kind_effect; should be normalized before inspecting *)                    
  | Flex of uvar_c_pattern * typ                             (* first type is a flex-pattern for a type-indexed computation; second type is the result type *)
and comp = syntax<comp', unit>
and cflags = 
  | TOTAL 
  | MLEFFECT 
  | RETURN 
  | SOMETRIVIAL
and uvar_c = Unionfind.uvar<comp_typ_uvar_basis> 
and uvar_c_pattern = typ                                     (* a Typ_meta(Meta_uvar_t_app(t, (uv, ... => typ => Kind_effect))) *)
and comp_typ_uvar_basis = 
  | Floating 
  | Resolved of comp
and uvar_t = Unionfind.uvar<uvar_basis<typ,knd>>
and meta_t = 
  | Meta_pattern of typ * list<arg>
  | Meta_named of typ * lident                               (* Useful for pretty printing to keep the type abbreviation around *)
  | Meta_comp of comp                                        (* Promoting a computation to a type, just for instantiating flex comp-vars with comp-lambdas *)
 //remove 
  | Meta_uvar_t_app      of typ * (uvar_t * knd)             (* Application of a uvar to some terms 'U (t|e)_1 ... (t|e)_n *)  
and uvar_basis<'a,'b> = 
  | Uvar of ('a -> 'b -> bool)                               (* A well-formedness check to ensure that all names are in scope *)
  | Fixed of 'a
and exp' =
  | Exp_bvar       of bvvar
  | Exp_fvar       of fvvar * bool                               (* flag indicates a constructor *)
  | Exp_constant   of sconst
  | Exp_abs        of binders * exp 
  | Exp_app        of exp * args                                 (* args in reverse order *)
  | Exp_match      of exp * list<(pat * option<exp> * exp)>      (* optional when clause in each equation *)
  | Exp_ascribed   of exp * typ 
  | Exp_let        of letbindings * exp                          (* let (rec?) x1 = e1 AND ... AND xn = en in e *)
  | Exp_uvar       of uvar_e * typ                               (* not present after 1st round tc *)
  | Exp_delayed    of exp * subst * memo<exp>                    (* A delayed substitution --- always force it before inspecting the first arg *)
  | Exp_meta       of meta_e                                     (* No longer tag every expression with info, only selectively *)
and exp = syntax<exp',typ>
and meta_e = 
  | Meta_desugared     of exp * meta_source_info                 (* Node tagged with some information about source term before desugaring *)
  | Meta_datainst      of exp * option<typ>                      (* Expect the data constructor e to build a t-typed value; only used internally to pretyping; not visible elsewhere *)
 //remove
  | Meta_uvar_e_app    of exp * (uvar_e * typ)                   (* Application of a uvar to some terms 'U (t|e)_1 ... (t|e)_n *)  
and meta_source_info =
  | Data_app
  | Sequence                   
  | Primop                                  (* ... add more cases here as needed for better code generation *)
and uvar_e = Unionfind.uvar<uvar_basis<exp,typ>>
and btvdef = bvdef<typ>
and bvvdef = bvdef<exp>
and pat = 
  | Pat_cons     of lident * list<pat>
  | Pat_var      of bvvdef
  | Pat_tvar     of btvdef
  | Pat_constant of sconst
  | Pat_disj     of list<pat>
  | Pat_wild
  | Pat_twild
  | Pat_withinfo of pat * Range.range
and knd' =
  | Kind_type
  | Kind_effect
  | Kind_abbrev of kabbrev * knd                          (* keep the abbreviation around for printing *)
  | Kind_arrow of binders * knd                           (* (ai:ki|xi:ti) => k' *)
  | Kind_uvar of uvar_k_app                               (* not present after 1st round tc *)
  | Kind_lam of binders * knd                             (* not present after 1st round tc *)
  | Kind_delayed of knd * subst * memo<knd>               (* delayed substitution --- always force before inspecting first element *)
  | Kind_unknown                                          (* not present after 1st round tc *)
and knd = syntax<knd', unit>
and uvar_k_app = uvar_k * args
and kabbrev = lident * args
and uvar_k = Unionfind.uvar<uvar_basis<knd,unit>>
and lbname = either<bvvdef, lident>
and letbindings = bool * list<(lbname * typ * exp)> (* let recs may have more than one element; top-level lets have lidents *)
and subst' = list<subst_elt>
and subst = {
    subst:subst';
    subst_fvs:memo<freevars>;
}
and subst_map = Util.smap<either<typ, exp>>
and subst_elt = either<(btvdef*typ), (bvvdef*exp)>
and fvar = either<btvdef, bvvdef>
and freevars = {
  ftvs: set<btvar>;
  fxvs: set<bvvar>;
}
and uvars = {
  uvars_k: set<uvar_k>;
  uvars_t: set<(uvar_t*knd)>;
  uvars_e: set<(uvar_e*typ)>;
  uvars_c: set<(uvar_t*knd)>;
}
and syntax<'a,'b> = {
    n:'a;
    tk:'b;
    pos:Range.range;
    fvs:memo<freevars>;
    uvs:memo<uvars>;
}
and btvar = bvar<typ,knd>
and bvvar = bvar<exp,typ>
and ftvar = var<knd>
and fvvar = var<typ>

type ktec = 
    | K of knd
    | T of typ
    | E of exp
    | C of comp

type freevars_l = list<either<btvar,bvvar>>
type formula = typ
type formulae = list<typ>
val new_ftv_set: unit -> set<bvar<'a,'b>>
val new_uv_set: unit -> set<Unionfind.uvar<'a>>
val new_uvt_set: unit -> set<(Unionfind.uvar<'a> * 'b)>

type tparam =
  | Tparam_typ  of btvdef * knd (* idents for pretty printing *)
  | Tparam_term of bvvdef * typ

type qualifier = 
  | Private 
  | Public 
  | Assumption
  | Definition  
  | Query
  | Lemma
  | Logic
  | Discriminator of lident                          (* discriminator for a datacon l *)
  | Projector of lident * either<btvdef, bvvdef>     (* projector for datacon l's argument 'a or x *)
  | RecordType of list<ident>                        (* unmangled field names *)
  | RecordConstructor of list<ident>                 (* unmangled field names *)
  | ExceptionConstructor
  | Effect 
 
type monad_abbrev = {
  mabbrev:lident;
  parms:list<tparam>;
  def:typ
  }
type monad_order = {
  source:lident;
  target:lident;
  lift: typ
 }
type monad_lat = list<monad_order>
type monad_decl = {
    mname:lident;
    total:bool;
    signature:knd;
    ret:typ;
    bind_wp:typ;
    bind_wlp:typ;
    ite_wp:typ;
    ite_wlp:typ;
    wp_binop:typ;
    wp_as_type:typ;
    close_wp:typ;
    close_wp_t:typ;
    assert_p:typ;
    assume_p:typ;
    null_wp:typ;
    trivial:typ;
    abbrevs:list<sigelt> 
 }
and sigelt =
  | Sig_tycon          of lident * list<tparam> * knd * list<lident> * list<lident> * list<qualifier> * Range.range (* bool is for a prop, list<lident> identifies mutuals, second list<lident> are all the constructors *)
  | Sig_typ_abbrev     of lident * list<tparam> * knd * typ * list<qualifier> * Range.range 
  | Sig_datacon        of lident * typ * lident * list<qualifier> * Range.range  (* second lident is the name of the type this constructs *)
  | Sig_val_decl       of lident * typ * list<qualifier> * Range.range 
  | Sig_assume         of lident * formula * list<qualifier> * Range.range 
  | Sig_let            of letbindings * Range.range 
  | Sig_main           of exp * Range.range 
  | Sig_bundle         of list<sigelt> * Range.range  (* an inductive type is a bundle of all mutually defined Sig_tycons and Sig_datacons *)
  | Sig_monads         of list<monad_decl> * monad_lat * Range.range
type sigelts = list<sigelt>

type modul = {
  name: lident;
  declarations: sigelts;
  exports: sigelts;
  is_interface:bool
}
type path = list<string>

val syn: 'a -> 'b -> ('b -> 'a -> 'c) -> 'c
val dummyRange: range
val mk_ident: (string * range) -> ident
val id_of_text: string -> ident
val text_of_id: ident -> string
val text_of_path: path -> string
val lid_equals: lident -> lident -> Tot<bool>
val bvd_eq: bvdef<'a> -> bvdef<'a> -> Tot<bool>
val order_bvd: either<bvdef<'a>, bvdef<'b>> -> either<bvdef<'c>, bvdef<'d>> -> int
val range_of_lid: lident -> range
val range_of_lbname: lbname -> range
val lid_of_ids: list<ident> -> lident
val ids_of_lid: lident -> list<ident>
val lid_of_path: path -> range -> lident
val path_of_lid: lident -> path
val text_of_lid: lident -> string
val withsort: 'a -> 'b -> withinfo_t<'a,'b>

val ktype:knd
val keffect: knd
val kun:knd
val tun:typ
val no_fvs: freevars
val no_uvs: uvars
val freevars_of_list: list<either<btvar, bvvar>> -> freevars
val list_of_freevars: freevars -> list<either<btvar,bvvar>>

val mk_Kind_type: knd
val mk_Kind_effect:knd
val mk_Kind_abbrev: (kabbrev * knd) -> range -> knd
val mk_Kind_arrow: (binders * knd) -> range -> knd
val mk_Kind_arrow': (binder * knd) -> range -> knd
val mk_Kind_delayed: (knd * subst * memo<knd>) -> range -> knd
val mk_Kind_uvar: uvar_k_app -> range -> knd
val mk_Kind_lam: (binders * knd) -> range -> knd

val mk_Typ_btvar: btvar -> knd -> range -> typ
val mk_Typ_const: ftvar -> knd -> range -> typ
val mk_Typ_fun: (binders * comp) -> knd -> range -> typ
val mk_Typ_refine: (bvvar * formula) -> knd -> range -> typ
val mk_Typ_app: (typ * args) -> knd -> range -> typ
val mk_Typ_app': (typ * arg) -> knd -> range -> typ
//val mk_Typ_dep: (typ * exp * bool) -> knd -> range -> typ
val mk_Typ_lam: (binders * typ) -> knd -> range -> typ
//val mk_Typ_tlam: (btvdef * knd * typ) -> knd -> range -> typ
val mk_Typ_ascribed': (typ * knd) -> knd -> range -> typ
val mk_Typ_ascribed: (typ * knd) -> range -> typ
val mk_Typ_meta': meta_t -> knd -> range -> typ
val mk_Typ_meta: meta_t -> typ
val mk_Typ_uvar': (uvar_t * knd) -> knd -> range -> typ
val mk_Typ_uvar: (uvar_t * knd) -> range -> typ
val mk_Typ_delayed: (typ * subst * memo<typ>) -> knd -> range -> typ

val mk_Total: typ -> comp
val mk_Comp: comp_typ -> comp
val mk_Flex: (uvar_c_pattern * typ) -> comp
val mk_Rigid: typ -> comp

val mk_Exp_bvar: bvvar -> typ -> range -> exp
val mk_Exp_fvar: (fvvar * bool) -> typ -> range -> exp 
val mk_Exp_constant: sconst -> typ -> range -> exp
val mk_Exp_abs: (binders * exp) -> typ -> range -> exp
//val mk_Exp_tabs: (btvdef * knd * exp) -> typ -> range -> exp
val mk_Exp_app: (exp * args) -> typ -> range -> exp
val mk_Exp_app': (exp * arg) -> typ -> range -> exp
//val mk_Exp_tapp: (exp * typ) -> typ -> range -> exp
val mk_Exp_match: (exp * list<(pat * option<exp> * exp)>) -> typ -> range -> exp
val mk_Exp_ascribed': (exp * typ) -> typ -> range -> exp
val mk_Exp_ascribed: (exp * typ) -> range -> exp
val mk_Exp_let: (letbindings * exp) -> typ -> range -> exp
val mk_Exp_uvar': (uvar_e * typ) -> typ -> range -> exp
val mk_Exp_uvar: (uvar_e * typ) -> range -> exp
val mk_Exp_delayed: (exp * subst * memo<exp>) -> typ -> range -> exp
val mk_Exp_meta' : meta_e -> typ -> range -> exp
val mk_Exp_meta: meta_e -> exp

val mk_subst: subst' -> subst
val extend_subst: subst_elt -> subst -> subst





val pat_vars: range -> pat -> list<either<btvdef,bvvdef>>




