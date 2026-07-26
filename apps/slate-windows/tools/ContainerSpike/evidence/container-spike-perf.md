# W3-1 reading container — size and stability

`FlowDocument` + custom semantic peers, the container the spike selected. Stages measured separately because they have different owners: core parse is Rust behind FFI, build and layout are W3-1's, and the peer tree is the custom-peer design itself.

| corpus | MB | blocks | runs | parse ms | build ms | layout ms | peers | peer walk ms | peer name ms | model MB | peak MB | status |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|
| baseline 1k words / 20 links | 0.01 | 49 | 79 | 19 | 159 | 209 | 50 | 5 | 0 | 0.1 | 82 | ok |
| 10k words / 300 links | 0.09 | 455 | 967 | 24 | 199 | 299 | 552 | 14 | 3 | 0.8 | 134 | ok |
| 50k words / 1.5k links | 0.43 | 2296 | 4836 | 43 | 398 | 558 | 2759 | 56 | 20 | 3.8 | 158 | ok |
| 200k words / 6k links | 1.72 | 9122 | 19290 | 117 | 1930 | 810 | 10980 | 179 | 92 | 14.9 | 227 | ok |
| 800k words / 24k links (~5 MB) | 6.88 | 36381 | 77050 | 411 | 7428 | 911 | 43848 | 554 | 166 | 59.5 | 609 | ok |
| 10k words / 2k huge destinations (~5 MB) | 4.85 | 455 | 4311 | 155 | 358 | 784 | 2252 | 24 | 15 | 39.4 | 228 | ok |
