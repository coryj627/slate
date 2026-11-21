177 documents, 174 with a decode time; 3 refused by the guard today (no decode).

**One estimated decode time**

| limit | refused, decodes under 5 s | of which under 1 s | admitted, decodes past 5 s | of which past 20 s | refused in all |
|---|---|---|---|---|---|
| 1 s | 37 | 4 | 0 | 0 | 79 |
| 2 s | 20 | 3 | 0 | 0 | 62 |
| 3 s | 14 | 2 | 2 | 0 | 54 |
| 5 s | 6 | 1 | 5 | 0 | 43 |
| 10 s | 3 | 0 | 18 | 0 | 27 |

One estimate, limit 1 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 3 documents; fastest A_attr R=1000 E=3000, estimate 3.38 s, actual 0.25 s
- A_class: 1 documents; fastest A_class R=5000 E=6000, estimate 2.83 s, actual 2.62 s
- A_type: 2 documents; fastest A_type R=1000 E=1000, estimate 1.13 s, actual 1.01 s
- A_use: 3 documents; fastest A_use R=1 G=4 U=3000, estimate 2.24 s, actual 2.76 s
- Big_style: 1 documents; fastest Big_style N=383126, estimate 1.83 s, actual 1.83 s
- C_double: 2 documents; fastest C_double D=16 E=1000, estimate 6.29 s, actual 0.03 s
- C_kary: 1 documents; fastest C_kary K=3 D=16 E=1, estimate 2.75 s, actual 0.01 s
- C_per_rule: 1 documents; fastest C_per_rule R=3000 E=5000, estimate 2.83 s, actual 2.56 s
- C_root: 1 documents; fastest C_root R=3000 E=5000, estimate 1.43 s, actual 1.27 s
- C_rule_all: 2 documents; fastest C_rule_all R=500 E=2000, estimate 1.03 s, actual 0.35 s
- C_typedecl: 1 documents; fastest C_typedecl R=1000 E=3000, estimate 3.66 s, actual 3.04 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 7.32 s, actual 1.40 s
- D_circles_inherit: 2 documents; fastest D_circles_inherit M=10 N=3000, estimate 1.27 s, actual 1.76 s
- D_css: 1 documents; fastest D_css M=500 V=1000, estimate 1.33 s, actual 1.38 s
- D_inherit: 1 documents; fastest D_inherit M=500 V=1000, estimate 1.33 s, actual 1.23 s
- D_marker: 4 documents; fastest D_marker M=500 V=1000, estimate 1.33 s, actual 1.29 s
- D_path: 1 documents; fastest D_path M=500 V=1000, estimate 1.33 s, actual 1.28 s
- D_paths: 3 documents; fastest D_paths M=1 N=20000, estimate 1.22 s, actual 2.25 s
- D_rects_inherit: 2 documents; fastest D_rects_inherit M=10 N=3000, estimate 1.27 s, actual 1.73 s
- D_shorthand: 1 documents; fastest D_shorthand M=500 V=1000, estimate 1.33 s, actual 1.22 s
- D_use: 1 documents; fastest D_use M=500 V=1000, estimate 1.33 s, actual 1.67 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s
- G_scatter: 1 documents; fastest G_scatter N=3000, estimate 2.24 s, actual 1.75 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- none

One estimate, limit 2 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 3 documents; fastest A_attr R=1000 E=3000, estimate 3.38 s, actual 0.25 s
- A_class: 1 documents; fastest A_class R=5000 E=6000, estimate 2.83 s, actual 2.62 s
- A_type: 1 documents; fastest A_type R=1000 E=3000, estimate 3.38 s, actual 2.69 s
- A_use: 3 documents; fastest A_use R=1 G=4 U=3000, estimate 2.24 s, actual 2.76 s
- C_double: 2 documents; fastest C_double D=16 E=1000, estimate 6.29 s, actual 0.03 s
- C_kary: 1 documents; fastest C_kary K=3 D=16 E=1, estimate 2.75 s, actual 0.01 s
- C_per_rule: 1 documents; fastest C_per_rule R=3000 E=5000, estimate 2.83 s, actual 2.56 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 10.3 s, actual 2.94 s
- C_typedecl: 1 documents; fastest C_typedecl R=1000 E=3000, estimate 3.66 s, actual 3.04 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 7.32 s, actual 1.40 s
- D_marker: 3 documents; fastest D_marker M=100 V=8002 L=1, estimate 2.47 s, actual 2.54 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s
- G_scatter: 1 documents; fastest G_scatter N=3000, estimate 2.24 s, actual 1.75 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- none

One estimate, limit 3 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 3 documents; fastest A_attr R=1000 E=3000, estimate 3.38 s, actual 0.25 s
- A_type: 1 documents; fastest A_type R=1000 E=3000, estimate 3.38 s, actual 2.69 s
- A_use: 2 documents; fastest A_use R=10000 G=4 U=100, estimate 3.85 s, actual 3.38 s
- C_double: 2 documents; fastest C_double D=16 E=1000, estimate 6.29 s, actual 0.03 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 10.3 s, actual 2.94 s
- C_typedecl: 1 documents; fastest C_typedecl R=1000 E=3000, estimate 3.66 s, actual 3.04 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 7.32 s, actual 1.40 s
- D_marker: 2 documents; fastest D_marker M=100 V=1000 L=10, estimate 3.08 s, actual 3.05 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_use: 1 documents; slowest A_use R=1 G=40 U=3000, estimate 2.26 s, actual 5.16 s
- D_marker: 1 documents; slowest D_marker M=100 V=10 L=1000, estimate 2.72 s, actual 5.22 s

One estimate, limit 5 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 2 documents; fastest A_attr R=3000 E=6000, estimate 20.2 s, actual 1.26 s
- C_double: 1 documents; fastest C_double D=16 E=1000, estimate 6.29 s, actual 0.03 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 10.3 s, actual 2.94 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 7.32 s, actual 1.40 s
- D_marker: 1 documents; fastest D_marker M=1000 V=2000, estimate 5.20 s, actual 4.71 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_compound: 1 documents; slowest A_compound R=5000 E=10000, estimate 4.71 s, actual 5.26 s
- A_desc: 1 documents; slowest A_desc R=5000 E=10000, estimate 4.71 s, actual 9.69 s
- A_use: 2 documents; slowest A_use R=1000 G=40 U=1000, estimate 4.06 s, actual 6.78 s
- D_marker: 1 documents; slowest D_marker M=100 V=10 L=1000, estimate 2.72 s, actual 5.22 s

One estimate, limit 10 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 2 documents; fastest A_attr R=3000 E=6000, estimate 20.2 s, actual 1.26 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 10.3 s, actual 2.94 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_class: 3 documents; slowest A_class R=5000 E=20000, estimate 9.39 s, actual 10.1 s
- A_compound: 1 documents; slowest A_compound R=5000 E=10000, estimate 4.71 s, actual 5.26 s
- A_desc: 1 documents; slowest A_desc R=5000 E=10000, estimate 4.71 s, actual 9.69 s
- A_list: 1 documents; slowest A_list R=2500 S=4 E=10000, estimate 9.37 s, actual 9.34 s
- A_type: 1 documents; slowest A_type R=4200 E=1700, estimate 8.04 s, actual 6.50 s
- A_unclassed: 1 documents; slowest A_unclassed R=10000 E=10000, estimate 9.40 s, actual 8.82 s
- A_unmatched: 1 documents; slowest A_unmatched R=10000 E=10000, estimate 9.40 s, actual 5.89 s
- A_use: 2 documents; slowest A_use R=1000 G=40 U=1000, estimate 4.06 s, actual 6.78 s
- C_double: 1 documents; slowest C_double D=15 E=1000, estimate 6.29 s, actual 7.43 s
- C_root: 1 documents; slowest C_root R=10000 E=10000, estimate 9.42 s, actual 9.64 s
- D_marker: 3 documents; slowest D_marker M=200 V=12500, estimate 7.04 s, actual 6.43 s
- D_use: 1 documents; slowest D_use M=1500 V=2000, estimate 7.76 s, actual 11.8 s
- D_use_poly: 1 documents; slowest D_use_poly M=100 V=1000 U=30, estimate 9.55 s, actual 8.77 s

**Named documents**

| document | estimate s | actual s | actual / estimate | 1 s | 2 s | 3 s | 5 s | 10 s |
|---|---|---|---|---|---|---|---|---|
| A_star_g R=3000 E=13000 | 43.8 | 40.5 | 0.92 | refused | refused | refused | refused | refused |
| A_star_rect R=15000 E=8000 | 134.9 | 135.6 | 1.01 | refused | refused | refused | refused | refused |
| C_double D=15 E=5000 | 31.5 | 34.8 | 1.11 | refused | refused | refused | refused | refused |
| D_marker M=2000 V=10000 | 51.5 | 50.0 | 0.97 | refused | refused | refused | refused | refused |
| G_scatter N=1000 | 0.25 | 0.27 | 1.04 | admitted | admitted | admitted | admitted | admitted |
| G_scatter N=3000 | 2.24 | 1.75 | 0.78 | refused | refused | admitted | admitted | admitted |
| G_scatter N=10000 | 24.7 | 17.4 | 0.71 | refused | refused | refused | refused | refused |
| G_scatter N=10000 nostyle=1 | 0.00 | 1.00 | n/a | admitted | admitted | admitted | admitted | admitted |
| D_rects_inherit M=100 N=1000 | 1.80 | 2.59 | 1.44 | refused | admitted | admitted | admitted | admitted |
| D_rects_inherit M=10 N=3000 | 1.27 | 1.73 | 1.36 | refused | admitted | admitted | admitted | admitted |
| D_circles_inherit M=100 N=1000 | 1.80 | 2.58 | 1.43 | refused | admitted | admitted | admitted | admitted |
| real mermaid_class_largest_n476.svg | 0.03 | 0.73 | 25.21 | admitted | admitted | admitted | admitted | admitted |
| real mermaid_er_largest_n451.svg | 0.00 | 0.76 | n/a | admitted | admitted | admitted | admitted | admitted |
| real mermaid_flowchart_largest_n643.svg | 0.00 | 0.21 | n/a | admitted | admitted | admitted | admitted | admitted |
| real mermaid_flowchart_styled_largest_n904.svg | 0.00 | 0.29 | n/a | admitted | admitted | admitted | admitted | admitted |
| real mermaid_sequence_largest_n1093.svg | 0.08 | 0.43 | 5.68 | admitted | admitted | admitted | admitted | admitted |
| real mermaid_state_largest_n501.svg | 0.04 | 0.32 | 8.39 | admitted | admitted | admitted | admitted | admitted |

**Actual over estimate**, the 128 decoded documents whose estimate is at least 10 ms: range 0.002 to 25.21, geometric mean 0.90, gsd 2.92.
Documents where both are at least 0.25 s (102): range 0.062 to 3.18, geometric mean 0.94, gsd 1.76.

| kind | documents | min | max | geometric mean | gsd |
|---|---|---|---|---|---|
| A_attr | 3 | 0.062 | 0.07 | 0.07 | 1.07 |
| A_class | 9 | 0.897 | 2.33 | 1.09 | 1.32 |
| A_compound | 2 | 0.995 | 1.12 | 1.05 | 1.06 |
| A_decls | 1 | 1.021 | 1.02 | 1.02 | 1.00 |
| A_desc | 2 | 1.915 | 2.06 | 1.99 | 1.04 |
| A_list | 2 | 0.996 | 1.03 | 1.01 | 1.02 |
| A_star_g | 1 | 0.924 | 0.92 | 0.92 | 1.00 |
| A_star_rect | 1 | 1.005 | 1.01 | 1.01 | 1.00 |
| A_type | 10 | 0.786 | 1.25 | 0.91 | 1.16 |
| A_unclassed | 2 | 0.877 | 0.94 | 0.91 | 1.03 |
| A_unmatched | 2 | 0.615 | 0.63 | 0.62 | 1.01 |
| A_use | 12 | 0.864 | 2.28 | 1.18 | 1.34 |
| Big_style | 2 | 0.800 | 1.00 | 0.90 | 1.12 |
| C_double | 9 | 0.005 | 2.53 | 0.44 | 8.31 |
| C_kary | 5 | 0.002 | 1.00 | 0.27 | 11.11 |
| C_per_rule | 3 | 0.903 | 0.99 | 0.95 | 1.04 |
| C_root | 3 | 0.892 | 1.49 | 1.11 | 1.24 |
| C_rule_all | 3 | 0.285 | 0.34 | 0.31 | 1.08 |
| C_triple | 3 | 2.262 | 2.89 | 2.58 | 1.11 |
| C_typedecl | 2 | 0.735 | 0.83 | 0.78 | 1.06 |
| C_typevar | 3 | 0.191 | 0.21 | 0.20 | 1.04 |
| D_circles_inherit | 3 | 0.778 | 1.43 | 1.16 | 1.32 |
| D_css | 1 | 1.038 | 1.04 | 1.04 | 1.00 |
| D_inherit | 1 | 0.928 | 0.93 | 0.93 | 1.00 |
| D_marker | 14 | 0.650 | 1.92 | 0.96 | 1.25 |
| D_path | 1 | 0.962 | 0.96 | 0.96 | 1.00 |
| D_paths | 14 | 0.877 | 3.18 | 1.43 | 1.42 |
| D_rects_attr | 1 | 0.912 | 0.91 | 0.91 | 1.00 |
| D_rects_inherit | 2 | 1.360 | 1.44 | 1.40 | 1.03 |
| D_shorthand | 1 | 0.917 | 0.92 | 0.92 | 1.00 |
| D_use | 2 | 1.258 | 1.52 | 1.38 | 1.10 |
| D_use_poly | 2 | 0.867 | 0.92 | 0.89 | 1.03 |
| G_scatter | 3 | 0.705 | 1.04 | 0.83 | 1.18 |
| real (mermaid) | 3 | 5.681 | 25.21 | 10.63 | 1.88 |

Estimate under 10 ms (46 documents; their time is the base cost no term carries), slowest first:

- Big_rects N=150092: estimate 0.0 ms, actual 2449 ms
- A_use_nostyle G=4 U=10000: estimate 0.0 ms, actual 2020 ms
- G_scatter N=10000 nostyle=1: estimate 0.0 ms, actual 998 ms
- real mermaid_er_largest_n451.svg: estimate 0.0 ms, actual 755 ms
- A_plain E=50000: estimate 0.0 ms, actual 653 ms
- Big_points N=1119023: estimate 0.0 ms, actual 555 ms
- D_paths M=1 N=20000 nomark=1: estimate 0.0 ms, actual 418 ms
- real mermaid_flowchart_styled_largest_n904.svg: estimate 0.0 ms, actual 293 ms
- D_paths M=1 N=20000 group=100 nomark=1: estimate 0.0 ms, actual 249 ms
- D_paths M=1 N=10000 nomark=1: estimate 0.0 ms, actual 216 ms
- real mermaid_flowchart_largest_n643.svg: estimate 0.0 ms, actual 212 ms
- A_plain E=20000: estimate 0.0 ms, actual 206 ms

**Margin to the largest real estimate** (real mermaid_sequence_largest_n1093.svg: 76.4 ms, actual 434 ms):

| limit | limit / largest real estimate |
|---|---|
| 1 s | 13x |
| 2 s | 26x |
| 3 s | 39x |
| 5 s | 65x |
| 10 s | 131x |

**Two per-product limits (stylesheet two-term, markers), uses and var() not guarded**

| limit | refused, decodes under 5 s | of which under 1 s | admitted, decodes past 5 s | of which past 20 s | refused in all |
|---|---|---|---|---|---|
| 1 s | 29 | 2 | 10 | 4 | 61 |
| 2 s | 13 | 1 | 10 | 4 | 45 |
| 5 s | 5 | 0 | 13 | 4 | 34 |

Two limits, limit 1 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 3 documents; fastest A_attr R=1000 E=3000, estimate 3.38 s, actual 0.25 s
- A_class: 1 documents; fastest A_class R=5000 E=6000, estimate 2.83 s, actual 2.62 s
- A_type: 2 documents; fastest A_type R=1000 E=1000, estimate 1.13 s, actual 1.01 s
- C_per_rule: 1 documents; fastest C_per_rule R=3000 E=5000, estimate 2.83 s, actual 2.56 s
- C_root: 1 documents; fastest C_root R=3000 E=5000, estimate 1.43 s, actual 1.27 s
- C_rule_all: 2 documents; fastest C_rule_all R=500 E=2000, estimate 1.03 s, actual 0.35 s
- C_typedecl: 1 documents; fastest C_typedecl R=1000 E=3000, estimate 3.66 s, actual 3.04 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 7.32 s, actual 1.40 s
- D_circles_inherit: 2 documents; fastest D_circles_inherit M=10 N=3000, estimate 1.27 s, actual 1.76 s
- D_css: 1 documents; fastest D_css M=500 V=1000, estimate 1.33 s, actual 1.38 s
- D_inherit: 1 documents; fastest D_inherit M=500 V=1000, estimate 1.33 s, actual 1.23 s
- D_marker: 4 documents; fastest D_marker M=500 V=1000, estimate 1.33 s, actual 1.29 s
- D_path: 1 documents; fastest D_path M=500 V=1000, estimate 1.33 s, actual 1.28 s
- D_paths: 3 documents; fastest D_paths M=1 N=20000, estimate 1.22 s, actual 2.25 s
- D_rects_inherit: 2 documents; fastest D_rects_inherit M=10 N=3000, estimate 1.27 s, actual 1.73 s
- D_shorthand: 1 documents; fastest D_shorthand M=500 V=1000, estimate 1.33 s, actual 1.22 s
- D_use: 1 documents; fastest D_use M=500 V=1000, estimate 1.33 s, actual 1.67 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_use: 7 documents; slowest A_use R=3000 G=4 U=3000, estimate 36.4 s, actual 33.4 s
- C_double: 2 documents; slowest C_double D=15 E=5000, estimate 31.5 s, actual 34.8 s
- G_scatter: 1 documents; slowest G_scatter N=10000, estimate 24.7 s, actual 17.4 s

Two limits, limit 2 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 3 documents; fastest A_attr R=1000 E=3000, estimate 3.38 s, actual 0.25 s
- A_class: 1 documents; fastest A_class R=5000 E=6000, estimate 2.83 s, actual 2.62 s
- A_type: 1 documents; fastest A_type R=1000 E=3000, estimate 3.38 s, actual 2.69 s
- C_per_rule: 1 documents; fastest C_per_rule R=3000 E=5000, estimate 2.83 s, actual 2.56 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 10.3 s, actual 2.94 s
- C_typedecl: 1 documents; fastest C_typedecl R=1000 E=3000, estimate 3.66 s, actual 3.04 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 7.32 s, actual 1.40 s
- D_marker: 3 documents; fastest D_marker M=100 V=8002 L=1, estimate 2.47 s, actual 2.54 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_use: 7 documents; slowest A_use R=3000 G=4 U=3000, estimate 36.4 s, actual 33.4 s
- C_double: 2 documents; slowest C_double D=15 E=5000, estimate 31.5 s, actual 34.8 s
- G_scatter: 1 documents; slowest G_scatter N=10000, estimate 24.7 s, actual 17.4 s

Two limits, limit 5 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 2 documents; fastest A_attr R=3000 E=6000, estimate 20.2 s, actual 1.26 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 10.3 s, actual 2.94 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 7.32 s, actual 1.40 s
- D_marker: 1 documents; fastest D_marker M=1000 V=2000, estimate 5.20 s, actual 4.71 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_compound: 1 documents; slowest A_compound R=5000 E=10000, estimate 4.71 s, actual 5.26 s
- A_desc: 1 documents; slowest A_desc R=5000 E=10000, estimate 4.71 s, actual 9.69 s
- A_use: 7 documents; slowest A_use R=3000 G=4 U=3000, estimate 36.4 s, actual 33.4 s
- C_double: 2 documents; slowest C_double D=15 E=5000, estimate 31.5 s, actual 34.8 s
- D_marker: 1 documents; slowest D_marker M=100 V=10 L=1000, estimate 2.72 s, actual 5.22 s
- G_scatter: 1 documents; slowest G_scatter N=10000, estimate 24.7 s, actual 17.4 s

Admitted by the two limits but refused by one estimate at the same number:

- 1 s: C_double D=15 E=5000 (estimate 31.5 s, actual 34.8 s); A_use R=3000 G=4 U=3000 (estimate 36.4 s, actual 33.4 s); A_use R=100 G=4 U=10000 (estimate 28.5 s, actual 32.5 s); A_use R=1 G=4 U=10000 (estimate 24.7 s, actual 30.3 s); G_scatter N=10000 (estimate 24.7 s, actual 17.4 s); A_use R=1 G=4 U=3000 X=20000 (estimate 17.0 s, actual 16.6 s); A_use R=2000 G=4 U=2000 (estimate 16.2 s, actual 14.0 s); C_double D=15 E=1000 (estimate 6.29 s, actual 7.43 s); A_use R=1000 G=40 U=1000 (estimate 4.06 s, actual 6.78 s); A_use R=1 G=40 U=3000 (estimate 2.26 s, actual 5.16 s); A_use R=1000 G=4 U=1000 (estimate 4.04 s, actual 3.80 s); A_use R=10000 G=4 U=100 (estimate 3.85 s, actual 3.38 s); C_double D=14 E=1000 (estimate 3.15 s, actual 3.19 s); A_use R=1 G=4 U=3000 (estimate 2.24 s, actual 2.76 s); Big_style N=383126 (estimate 1.83 s, actual 1.83 s); G_scatter N=3000 (estimate 2.24 s, actual 1.75 s); C_double D=16 E=1000 (estimate 6.29 s, actual 0.03 s); C_kary K=3 D=16 E=1 (estimate 2.75 s, actual 0.01 s)
- 2 s: C_double D=15 E=5000 (estimate 31.5 s, actual 34.8 s); A_use R=3000 G=4 U=3000 (estimate 36.4 s, actual 33.4 s); A_use R=100 G=4 U=10000 (estimate 28.5 s, actual 32.5 s); A_use R=1 G=4 U=10000 (estimate 24.7 s, actual 30.3 s); G_scatter N=10000 (estimate 24.7 s, actual 17.4 s); A_use R=1 G=4 U=3000 X=20000 (estimate 17.0 s, actual 16.6 s); A_use R=2000 G=4 U=2000 (estimate 16.2 s, actual 14.0 s); C_double D=15 E=1000 (estimate 6.29 s, actual 7.43 s); A_use R=1000 G=40 U=1000 (estimate 4.06 s, actual 6.78 s); A_use R=1 G=40 U=3000 (estimate 2.26 s, actual 5.16 s); A_use R=1000 G=4 U=1000 (estimate 4.04 s, actual 3.80 s); A_use R=10000 G=4 U=100 (estimate 3.85 s, actual 3.38 s); C_double D=14 E=1000 (estimate 3.15 s, actual 3.19 s); A_use R=1 G=4 U=3000 (estimate 2.24 s, actual 2.76 s); G_scatter N=3000 (estimate 2.24 s, actual 1.75 s); C_double D=16 E=1000 (estimate 6.29 s, actual 0.03 s); C_kary K=3 D=16 E=1 (estimate 2.75 s, actual 0.01 s)
- 5 s: C_double D=15 E=5000 (estimate 31.5 s, actual 34.8 s); A_use R=3000 G=4 U=3000 (estimate 36.4 s, actual 33.4 s); A_use R=100 G=4 U=10000 (estimate 28.5 s, actual 32.5 s); A_use R=1 G=4 U=10000 (estimate 24.7 s, actual 30.3 s); G_scatter N=10000 (estimate 24.7 s, actual 17.4 s); A_use R=1 G=4 U=3000 X=20000 (estimate 17.0 s, actual 16.6 s); A_use R=2000 G=4 U=2000 (estimate 16.2 s, actual 14.0 s); C_double D=15 E=1000 (estimate 6.29 s, actual 7.43 s); C_double D=16 E=1000 (estimate 6.29 s, actual 0.03 s)

Refused by the two limits but admitted by one estimate at the same number:

- 1 s: none
- 2 s: none
- 5 s: none

**One estimate, variant 2 (attribute keys, combinator weight 2, custom and var() declarations at 0.5 us, var() giving up past its depth, the use instance term)**

| limit | refused, decodes under 5 s | of which under 1 s | admitted, decodes past 5 s | of which past 20 s | refused in all |
|---|---|---|---|---|---|
| 1 s | 34 | 0 | 0 | 0 | 76 |
| 2 s | 16 | 0 | 0 | 0 | 58 |
| 3 s | 10 | 0 | 1 | 0 | 51 |
| 5 s | 2 | 0 | 2 | 0 | 42 |
| 10 s | 0 | 0 | 18 | 0 | 24 |

Variant 2, limit 1 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 2 documents; fastest A_attr R=3000 E=6000, estimate 1.70 s, actual 1.26 s
- A_class: 1 documents; fastest A_class R=5000 E=6000, estimate 2.83 s, actual 2.62 s
- A_desc: 1 documents; fastest A_desc R=2000 E=5000, estimate 1.89 s, actual 1.82 s
- A_type: 2 documents; fastest A_type R=1000 E=1000, estimate 1.13 s, actual 1.01 s
- A_use: 3 documents; fastest A_use R=1 G=4 U=3000, estimate 2.57 s, actual 2.76 s
- Big_style: 1 documents; fastest Big_style N=383126, estimate 1.83 s, actual 1.83 s
- C_double: 1 documents; fastest C_double D=14 E=1000, estimate 3.15 s, actual 3.19 s
- C_per_rule: 1 documents; fastest C_per_rule R=3000 E=5000, estimate 2.83 s, actual 2.56 s
- C_root: 1 documents; fastest C_root R=3000 E=5000, estimate 1.42 s, actual 1.27 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 5.00 s, actual 2.94 s
- C_typedecl: 1 documents; fastest C_typedecl R=1000 E=3000, estimate 2.07 s, actual 3.04 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 4.14 s, actual 1.40 s
- D_circles_inherit: 2 documents; fastest D_circles_inherit M=10 N=3000, estimate 1.27 s, actual 1.76 s
- D_css: 1 documents; fastest D_css M=500 V=1000, estimate 1.33 s, actual 1.38 s
- D_inherit: 1 documents; fastest D_inherit M=500 V=1000, estimate 1.33 s, actual 1.23 s
- D_marker: 4 documents; fastest D_marker M=500 V=1000, estimate 1.33 s, actual 1.29 s
- D_path: 1 documents; fastest D_path M=500 V=1000, estimate 1.33 s, actual 1.28 s
- D_paths: 3 documents; fastest D_paths M=1 N=20000, estimate 1.22 s, actual 2.25 s
- D_rects_inherit: 2 documents; fastest D_rects_inherit M=10 N=3000, estimate 1.27 s, actual 1.73 s
- D_shorthand: 1 documents; fastest D_shorthand M=500 V=1000, estimate 1.33 s, actual 1.22 s
- D_use: 1 documents; fastest D_use M=500 V=1000, estimate 1.33 s, actual 1.67 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s
- G_scatter: 1 documents; fastest G_scatter N=3000, estimate 2.31 s, actual 1.75 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- none

Variant 2, limit 2 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 1 documents; fastest A_attr R=5000 E=10000, estimate 4.71 s, actual 3.73 s
- A_class: 1 documents; fastest A_class R=5000 E=6000, estimate 2.83 s, actual 2.62 s
- A_type: 1 documents; fastest A_type R=1000 E=3000, estimate 3.38 s, actual 2.69 s
- A_use: 3 documents; fastest A_use R=1 G=4 U=3000, estimate 2.57 s, actual 2.76 s
- C_double: 1 documents; fastest C_double D=14 E=1000, estimate 3.15 s, actual 3.19 s
- C_per_rule: 1 documents; fastest C_per_rule R=3000 E=5000, estimate 2.83 s, actual 2.56 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 5.00 s, actual 2.94 s
- C_typedecl: 1 documents; fastest C_typedecl R=1000 E=3000, estimate 2.07 s, actual 3.04 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 4.14 s, actual 1.40 s
- D_marker: 3 documents; fastest D_marker M=100 V=8002 L=1, estimate 2.47 s, actual 2.54 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s
- G_scatter: 1 documents; fastest G_scatter N=3000, estimate 2.31 s, actual 1.75 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- none

Variant 2, limit 3 s. Refused although they decode under 5 s (worst per kind: the fastest):

- A_attr: 1 documents; fastest A_attr R=5000 E=10000, estimate 4.71 s, actual 3.73 s
- A_type: 1 documents; fastest A_type R=1000 E=3000, estimate 3.38 s, actual 2.69 s
- A_use: 2 documents; fastest A_use R=10000 G=4 U=100, estimate 4.19 s, actual 3.38 s
- C_double: 1 documents; fastest C_double D=14 E=1000, estimate 3.15 s, actual 3.19 s
- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 5.00 s, actual 2.94 s
- C_typevar: 1 documents; fastest C_typevar R=1000 E=3000, estimate 4.14 s, actual 1.40 s
- D_marker: 2 documents; fastest D_marker M=100 V=1000 L=10, estimate 3.08 s, actual 3.05 s
- D_use_poly: 1 documents; fastest D_use_poly M=100 V=1000 U=10, estimate 3.39 s, actual 2.94 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- D_marker: 1 documents; slowest D_marker M=100 V=10 L=1000, estimate 2.72 s, actual 5.22 s

Variant 2, limit 5 s. Refused although they decode under 5 s (worst per kind: the fastest):

- C_rule_all: 1 documents; fastest C_rule_all R=2000 E=5000, estimate 5.00 s, actual 2.94 s
- D_marker: 1 documents; fastest D_marker M=1000 V=2000, estimate 5.20 s, actual 4.71 s

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_compound: 1 documents; slowest A_compound R=5000 E=10000, estimate 4.71 s, actual 5.26 s
- D_marker: 1 documents; slowest D_marker M=100 V=10 L=1000, estimate 2.72 s, actual 5.22 s

Variant 2, limit 10 s. Refused although they decode under 5 s (worst per kind: the fastest):

- none

Admitted although they decode past 5 s (worst per kind: the slowest):

- A_class: 3 documents; slowest A_class R=5000 E=20000, estimate 9.39 s, actual 10.1 s
- A_compound: 1 documents; slowest A_compound R=5000 E=10000, estimate 4.71 s, actual 5.26 s
- A_desc: 1 documents; slowest A_desc R=5000 E=10000, estimate 9.39 s, actual 9.69 s
- A_list: 1 documents; slowest A_list R=2500 S=4 E=10000, estimate 9.37 s, actual 9.34 s
- A_type: 1 documents; slowest A_type R=4200 E=1700, estimate 8.04 s, actual 6.50 s
- A_unclassed: 1 documents; slowest A_unclassed R=10000 E=10000, estimate 9.40 s, actual 8.82 s
- A_unmatched: 1 documents; slowest A_unmatched R=10000 E=10000, estimate 9.40 s, actual 5.89 s
- A_use: 2 documents; slowest A_use R=1000 G=40 U=1000, estimate 7.70 s, actual 6.78 s
- C_double: 1 documents; slowest C_double D=15 E=1000, estimate 6.29 s, actual 7.43 s
- C_root: 1 documents; slowest C_root R=10000 E=10000, estimate 9.41 s, actual 9.64 s
- D_marker: 3 documents; slowest D_marker M=200 V=12500, estimate 7.04 s, actual 6.43 s
- D_use: 1 documents; slowest D_use M=1500 V=2000, estimate 7.76 s, actual 11.8 s
- D_use_poly: 1 documents; slowest D_use_poly M=100 V=1000 U=30, estimate 9.55 s, actual 8.77 s

Variant 2, actual over estimate, the 125 documents estimated at 10 ms or more: range 0.337 to 25.21, geometric mean 1.08, gsd 1.67.
Both at least 0.25 s (102): range 0.337 to 3.18, geometric mean 0.99, gsd 1.36.

| kind | documents | min | max | geometric mean | gsd |
|---|---|---|---|---|---|
| A_attr | 3 | 0.740 | 0.85 | 0.79 | 1.06 |
| A_class | 9 | 0.897 | 2.33 | 1.09 | 1.32 |
| A_compound | 2 | 0.995 | 1.12 | 1.05 | 1.06 |
| A_decls | 1 | 1.021 | 1.02 | 1.02 | 1.00 |
| A_desc | 2 | 0.965 | 1.03 | 1.00 | 1.03 |
| A_list | 2 | 0.996 | 1.03 | 1.01 | 1.02 |
| A_star_g | 1 | 0.924 | 0.92 | 0.92 | 1.00 |
| A_star_rect | 1 | 1.005 | 1.01 | 1.01 | 1.00 |
| A_type | 10 | 0.786 | 1.25 | 0.91 | 1.16 |
| A_unclassed | 2 | 0.877 | 0.94 | 0.91 | 1.03 |
| A_unmatched | 2 | 0.615 | 0.63 | 0.62 | 1.01 |
| A_use | 12 | 0.788 | 1.18 | 0.96 | 1.15 |
| Big_style | 2 | 0.800 | 1.00 | 0.90 | 1.12 |
| C_double | 7 | 1.013 | 2.53 | 1.33 | 1.32 |
| C_kary | 4 | 0.745 | 1.00 | 0.89 | 1.12 |
| C_per_rule | 3 | 0.905 | 0.99 | 0.96 | 1.04 |
| C_root | 3 | 0.895 | 1.50 | 1.11 | 1.24 |
| C_rule_all | 3 | 0.586 | 0.70 | 0.64 | 1.07 |
| C_triple | 3 | 2.265 | 2.92 | 2.59 | 1.11 |
| C_typedecl | 2 | 1.301 | 1.47 | 1.38 | 1.06 |
| C_typevar | 3 | 0.337 | 0.37 | 0.36 | 1.04 |
| D_circles_inherit | 3 | 0.778 | 1.43 | 1.16 | 1.32 |
| D_css | 1 | 1.038 | 1.04 | 1.04 | 1.00 |
| D_inherit | 1 | 0.928 | 0.93 | 0.93 | 1.00 |
| D_marker | 14 | 0.650 | 1.92 | 0.96 | 1.25 |
| D_path | 1 | 0.962 | 0.96 | 0.96 | 1.00 |
| D_paths | 14 | 0.877 | 3.18 | 1.43 | 1.42 |
| D_rects_attr | 1 | 0.912 | 0.91 | 0.91 | 1.00 |
| D_rects_inherit | 2 | 1.360 | 1.44 | 1.40 | 1.03 |
| D_shorthand | 1 | 0.917 | 0.92 | 0.92 | 1.00 |
| D_use | 2 | 1.258 | 1.52 | 1.38 | 1.10 |
| D_use_poly | 2 | 0.867 | 0.92 | 0.89 | 1.03 |
| G_scatter | 3 | 0.699 | 0.96 | 0.80 | 1.14 |
| real (mermaid) | 3 | 5.681 | 25.21 | 10.63 | 1.88 |

Refused by today's guard (RenderedElements), never decoded:

- D_paths M=10 N=18000: estimate 1.93 s
- D_rects_attr M=100 N=1000: estimate 1.80 s
- D_paths M=10 N=20000: estimate 2.14 s
