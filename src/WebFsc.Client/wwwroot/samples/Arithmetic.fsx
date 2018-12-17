let rec fibo = function
    | 0 | 1 -> 1
    | n -> fibo (n - 1) + fibo (n - 2)

printfn "fibo(8) = %i" (fibo 8)
